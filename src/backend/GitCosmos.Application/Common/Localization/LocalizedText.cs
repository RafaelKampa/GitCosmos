using System.Globalization;

namespace GitCosmos.Application.Common.Localization;

public static class LocalizedText
{
    private static readonly Dictionary<string, Dictionary<string, string>> Messages = new()
    {
        ["pt-BR"] = new()
        {
            ["system.ready"] = "O GitCosmos está pronto para iniciar.",
            ["workspace.name_required"] = "Informe o nome do workspace.",
            ["workspace.name_length"] = "O nome do workspace deve ter no máximo 100 caracteres.",
            ["workspace.name_exists"] = "Já existe um workspace com esse nome.",
            ["workspace.created"] = "Workspace criado com sucesso.",
            ["workspace.pagination"] = "Use página entre 0 e 21474836 e tamanho entre 1 e 100.",
            ["error.internal"] = "Não foi possível concluir a operação.",
            ["error.bad_request"] = "A requisição é inválida.",
            ["error.unauthorized"] = "Acesso não autorizado.",
            ["error.conflict"] = "A operação conflita com os dados existentes.",
            ["error.origin"] = "A origem da requisição não é permitida.",
            ["model.invalid_value"] = "O campo '{0}' contém um valor inválido."
        },
        ["es"] = new()
        {
            ["system.ready"] = "GitCosmos está listo para comenzar.",
            ["workspace.name_required"] = "Indique el nombre del workspace.",
            ["workspace.name_length"] = "El nombre del workspace debe tener como máximo 100 caracteres.",
            ["workspace.name_exists"] = "Ya existe un workspace con ese nombre.",
            ["workspace.created"] = "Workspace creado correctamente.",
            ["workspace.pagination"] = "Use página entre 0 y 21474836 y tamaño entre 1 y 100.",
            ["error.internal"] = "No se pudo completar la operación.",
            ["error.bad_request"] = "La solicitud no es válida.",
            ["error.unauthorized"] = "Acceso no autorizado.",
            ["error.conflict"] = "La operación entra en conflicto con los datos existentes.",
            ["error.origin"] = "El origen de la solicitud no está permitido.",
            ["model.invalid_value"] = "El campo '{0}' contiene un valor no válido."
        },
        ["en"] = new()
        {
            ["system.ready"] = "GitCosmos is ready to start.",
            ["workspace.name_required"] = "Enter the workspace name.",
            ["workspace.name_length"] = "The workspace name must contain at most 100 characters.",
            ["workspace.name_exists"] = "A workspace with this name already exists.",
            ["workspace.created"] = "Workspace created successfully.",
            ["workspace.pagination"] = "Use a page between 0 and 21474836 and a size between 1 and 100.",
            ["error.internal"] = "The operation could not be completed.",
            ["error.bad_request"] = "The request is invalid.",
            ["error.unauthorized"] = "Unauthorized access.",
            ["error.conflict"] = "The operation conflicts with existing data.",
            ["error.origin"] = "The request origin is not allowed.",
            ["model.invalid_value"] = "The field '{0}' contains an invalid value."
        }
    };

    public static string Get(string key, params object[] args) =>
        string.Format(CultureInfo.GetCultureInfo(RequestLanguage.Current), Messages[RequestLanguage.Current][key], args);
}
