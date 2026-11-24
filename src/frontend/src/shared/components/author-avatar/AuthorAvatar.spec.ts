import { afterEach, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import AuthorAvatar from './AuthorAvatar.vue';
afterEach(() => vi.unstubAllGlobals());

it('uses a hash of the normalized email for the photo and falls back to initials on image errors', async () => {
  const digest = vi.fn().mockResolvedValue(new Uint8Array([0, 15, 255]).buffer);
  vi.stubGlobal('crypto', { subtle: { digest } });
  const view = mount(AuthorAvatar, { props: { name: 'Rafael Kampa', email: '  USER@EXAMPLE.COM ' } });
  await flushPromises();
  const photo = view.get('img');
  expect(digest).toHaveBeenCalledWith('SHA-256', new TextEncoder().encode('user@example.com'));
  expect(photo.attributes('src')).toBe('https://www.gravatar.com/avatar/000fff?s=48&d=404&r=g');
  expect(photo.attributes('src')).not.toContain('user@example.com');
  expect(photo.attributes('referrerpolicy')).toBe('no-referrer');
  await photo.trigger('error'); expect(view.find('img').exists()).toBe(false); expect(view.text()).toBe('RK');
  await view.setProps({ name: '  Ana  ', email: '' }); expect(view.text()).toBe('A');
  await view.setProps({ name: '' }); expect(view.text()).toBe('?');
  view.unmount();
});

it('keeps initials when hashing is unavailable', async () => {
  vi.stubGlobal('crypto', { subtle: { digest: vi.fn().mockRejectedValue(new Error('unavailable')) } });
  const view = mount(AuthorAvatar, { props: { name: 'José Silva', email: 'test@example.com' } });
  await flushPromises(); expect(view.find('img').exists()).toBe(false); expect(view.text()).toBe('JS');
  view.unmount();
});

it('ignores a hash result after the email changes or the component unmounts', async () => {
  let resolve!: (value: ArrayBuffer) => void;
  const digest = vi.fn().mockReturnValue(new Promise<ArrayBuffer>(done => { resolve = done; }));
  vi.stubGlobal('crypto', { subtle: { digest } });
  const view = mount(AuthorAvatar, { props: { name: 'Author', email: 'old@example.com' } });
  await view.setProps({ email: '' }); resolve(new Uint8Array([1]).buffer); await flushPromises();
  expect(view.find('img').exists()).toBe(false);
  let finish!: (value: ArrayBuffer) => void;
  digest.mockReturnValueOnce(new Promise<ArrayBuffer>(done => { finish = done; }));
  await view.setProps({ email: 'new@example.com' }); view.unmount(); finish(new Uint8Array([2]).buffer); await flushPromises();
});
