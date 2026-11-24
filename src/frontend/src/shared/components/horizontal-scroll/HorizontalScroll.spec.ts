import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import HorizontalScroll from './HorizontalScroll.vue';
let view: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => { view?.unmount(); view = undefined; vi.unstubAllGlobals(); vi.restoreAllMocks(); });

it('offers arrows only for overflow and scrolls in either direction without triggering parent clicks', async () => {
  vi.stubGlobal('ResizeObserver', undefined);
  vi.stubGlobal('matchMedia', vi.fn().mockReturnValue({ matches: false }));
  view = mount(HorizontalScroll, { props: { contentKey: 'long' }, slots: { default: '<strong>Long commit subject</strong>' } });
  expect(view.find('button').exists()).toBe(false);
  const viewport = view.get('.scroll-viewport').element as HTMLElement;
  Object.defineProperties(viewport, { clientWidth: { value: 200, configurable: true }, scrollWidth: { value: 600, configurable: true } });
  const scrollBy = vi.fn().mockImplementation((options: ScrollToOptions) => { viewport.scrollLeft += options.left!; viewport.dispatchEvent(new Event('scroll')); });
  Object.defineProperty(viewport, 'scrollBy', { value: scrollBy });
  window.dispatchEvent(new Event('resize')); await flushPromises();
  const parentClick = vi.fn(); view.element.addEventListener('click', parentClick);
  expect(view.get('.scroll-right').attributes('aria-controls')).toBe(viewport.id);
  await view.get('.scroll-right').trigger('click');
  expect(scrollBy).toHaveBeenLastCalledWith({ left: 160, behavior: 'smooth' }); expect(parentClick).not.toHaveBeenCalled();
  expect(view.find('.scroll-left').exists()).toBe(true);
  await view.get('.scroll-left').trigger('click'); expect(viewport.scrollLeft).toBe(0);
  expect(view.find('.scroll-left').exists()).toBe(false);
  viewport.scrollLeft = 400; await view.get('.scroll-viewport').trigger('scroll');
  expect(view.find('.scroll-right').exists()).toBe(false);
  Object.defineProperty(viewport, 'scrollWidth', { value: 200 }); viewport.scrollLeft = 0;
  await view.setProps({ contentKey: 'short' }); await flushPromises(); expect(view.find('button').exists()).toBe(false);
});

it('measures resize and content changes and respects reduced motion', async () => {
  const disconnect = vi.fn(); const observe = vi.fn(); let resized!: () => void;
  vi.stubGlobal('ResizeObserver', class { constructor(callback: () => void) { resized = callback; } observe = observe; disconnect = disconnect; });
  vi.stubGlobal('matchMedia', vi.fn().mockReturnValue({ matches: true }));
  view = mount(HorizontalScroll, { props: { contentKey: 'pills' }, slots: { default: '<span>main origin/main</span>' } });
  expect(observe).toHaveBeenCalledTimes(2);
  const viewport = view.get('.scroll-viewport').element as HTMLElement;
  Object.defineProperties(viewport, { clientWidth: { value: 20 }, scrollWidth: { value: 300 } });
  const scrollBy = vi.fn(); Object.defineProperty(viewport, 'scrollBy', { value: scrollBy });
  resized(); await flushPromises(); await view.get('.scroll-right').trigger('click');
  expect(scrollBy).toHaveBeenCalledWith({ left: 80, behavior: 'instant' });
  vi.stubGlobal('matchMedia', undefined); await view.get('.scroll-right').trigger('click');
  expect(scrollBy).toHaveBeenLastCalledWith({ left: 80, behavior: 'smooth' });
  await view.setProps({ contentKey: 'updated' }); view.unmount(); view = undefined; await flushPromises();
  expect(disconnect).toHaveBeenCalledOnce(); resized();
});
