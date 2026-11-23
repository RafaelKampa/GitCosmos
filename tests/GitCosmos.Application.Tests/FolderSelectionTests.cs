using GitCosmos.API;
using GitCosmos.API.Controllers;
using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.FileSystem;
using GitCosmos.Application.DTOs.Responses.FileSystem;
using GitCosmos.Application.Interfaces.FileSystem;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.Services;
using GitCosmos.Infrastructure.FileSystem;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GitCosmos.Application.Tests;

public class FolderSelectionTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task Service_LocalizesNativeLabelsTrimsInitialPathAndDistinguishesCancel(string language)
    {
        RequestLanguage.Current = language;
        using var source = new CancellationTokenSource();
        var picker = new Mock<ILocalFolderPicker>(MockBehavior.Strict);
        picker.Setup(x => x.PickAsync(LocalizedText.Get("folder.title"), LocalizedText.Get("folder.confirm"), @"C:\old", source.Token))
            .ReturnsAsync(@"C:\selected");
        var service = new LocalFolderService(picker.Object);
        var response = await service.SelectAsync(new(@"  C:\old  "), source.Token);
        Assert.True(response.Success);
        Assert.Equal(new FolderSelectionResponse(@"C:\selected", false), response.Result);
        picker.Setup(x => x.PickAsync(LocalizedText.Get("folder.title"), LocalizedText.Get("folder.confirm"), null, source.Token))
            .ReturnsAsync((string?)null);
        Assert.Equal(new FolderSelectionResponse(null, true), (await service.SelectAsync(new(null), source.Token)).Result);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SelectAsync(new(new string('x', 32768)), source.Token));
    }

    [Fact]
    public async Task ControllerAndCompositionRoot_PreserveContractsWithoutOpeningAnyWindow()
    {
        var service = new Mock<ILocalFolderService>(MockBehavior.Strict);
        using var source = new CancellationTokenSource();
        var request = new SelectFolderRequest(null);
        var controller = new LocalFolderController(service.Object);
        var selected = CustomResponse<FolderSelectionResponse>.Ok(new("path", false));
        service.Setup(x => x.SelectAsync(request, source.Token)).ReturnsAsync(selected);
        Assert.Same(selected, Assert.IsType<OkObjectResult>(await controller.SelectFolder(request, source.Token)).Value);
        var failed = CustomResponse<FolderSelectionResponse>.Fail("invalid");
        service.Setup(x => x.SelectAsync(request, source.Token)).ReturnsAsync(failed);
        Assert.Same(failed, Assert.IsType<BadRequestObjectResult>(await controller.SelectFolder(request, source.Token)).Value);
        await using var app = ApiTestHost.Build();
        using var scope = app.Services.CreateScope();
        Assert.IsType<LocalFolderService>(scope.ServiceProvider.GetRequiredService<ILocalFolderService>());
        Assert.IsType<WindowsFolderPicker>(scope.ServiceProvider.GetRequiredService<ILocalFolderPicker>());
        Assert.IsType<WindowsFolderDialogFactory>(scope.ServiceProvider.GetRequiredService<IWindowsFolderDialogFactory>());
    }

    [Fact]
    public async Task Picker_RunsOnStaDisposesTheDialogAndReleasesItsGate()
    {
        var factory = new Mock<IWindowsFolderDialogFactory>(MockBehavior.Strict);
        factory.SetupGet(x => x.IsSupported).Returns(true);
        var dialog = new FakeFolderDialog { Result = @"C:\chosen" };
        factory.Setup(x => x.Create()).Returns(dialog);
        var picker = new WindowsFolderPicker(factory.Object, NullLogger<WindowsFolderPicker>.Instance);
        Assert.Equal(@"C:\chosen", await picker.PickAsync("Title", "Select", @"C:\old"));
        Assert.Equal(ApartmentState.STA, dialog.Apartment);
        Assert.Equal(("Title", "Select", @"C:\old"), dialog.Arguments);
        Assert.True(dialog.Disposed);
        dialog = new FakeFolderDialog();
        factory.Setup(x => x.Create()).Returns(dialog);
        Assert.Null(await picker.PickAsync("Title", "Select", null));
        Assert.True(dialog.Disposed);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task Picker_ReportsUnavailableAndNativeFailuresInTheRequestLanguage(string language)
    {
        RequestLanguage.Current = language;
        var factory = new Mock<IWindowsFolderDialogFactory>(MockBehavior.Strict);
        factory.SetupGet(x => x.IsSupported).Returns(false);
        var picker = new WindowsFolderPicker(factory.Object, NullLogger<WindowsFolderPicker>.Instance);
        var unavailable = await Assert.ThrowsAsync<OperationFailedAppException>(() => picker.PickAsync("Title", "Select", null));
        Assert.Equal(LocalizedText.Get("folder.unavailable"), unavailable.Message);
        factory.SetupGet(x => x.IsSupported).Returns(true);
        factory.Setup(x => x.Create()).Throws(new InvalidOperationException("native diagnostic"));
        var failed = await Assert.ThrowsAsync<OperationFailedAppException>(() => picker.PickAsync("Title", "Select", null));
        Assert.Equal(LocalizedText.Get("folder.error"), failed.Message);
        factory.Setup(x => x.Create()).Returns(new FakeFolderDialog { FailOnShow = true });
        await Assert.ThrowsAsync<OperationFailedAppException>(() => picker.PickAsync("Title", "Select", null));
        factory.Setup(x => x.Create()).Returns(new FakeFolderDialog { Result = "ok" });
        Assert.Equal("ok", await picker.PickAsync("Title", "Select", null));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => picker.PickAsync("Title", "Select", null, new CancellationToken(true)));
    }

    [Fact]
    public async Task Picker_RejectsConcurrentWindowsAndClosesOnlyThePendingSelectionOnAbort()
    {
        var factory = new Mock<IWindowsFolderDialogFactory>(MockBehavior.Strict);
        factory.SetupGet(x => x.IsSupported).Returns(true);
        using var release = new ManualResetEventSlim();
        var dialog = new FakeFolderDialog { Release = release };
        factory.Setup(x => x.Create()).Returns(dialog);
        var picker = new WindowsFolderPicker(factory.Object, NullLogger<WindowsFolderPicker>.Instance);
        using var source = new CancellationTokenSource();
        var selection = picker.PickAsync("Title", "Select", null, source.Token);
        await dialog.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var busy = await Assert.ThrowsAsync<ConflictAppException>(() => picker.PickAsync("Title", "Select", null));
        Assert.Equal(LocalizedText.Get("folder.busy"), busy.Message);
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => selection);
        await dialog.Closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await dialog.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(dialog.Disposed);
    }

    private sealed class FakeFolderDialog : IWindowsFolderDialog
    {
        public string? Result { get; init; }
        public bool FailOnShow { get; init; }
        public bool Disposed { get; private set; }
        public ApartmentState Apartment { get; private set; }
        public (string, string, string?) Arguments { get; private set; }
        public ManualResetEventSlim? Release { get; init; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? Show(string title, string confirmLabel, string? initialPath)
        {
            Apartment = Thread.CurrentThread.GetApartmentState();
            Arguments = (title, confirmLabel, initialPath);
            Started.TrySetResult();
            if (FailOnShow) throw new InvalidOperationException("native diagnostic");
            if (Release != null) Assert.True(Release.Wait(TimeSpan.FromSeconds(10)));
            return Result;
        }
        public void Close() { Release?.Set(); Closed.TrySetResult(); }
        public void Dispose() { Disposed = true; Completed.TrySetResult(); }
    }
}
