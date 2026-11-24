import { afterEach, expect, it, vi } from 'vitest';
import { mount, type VueWrapper } from '@vue/test-utils';
import ResizeHandle from './ResizeHandle.vue';
let view: VueWrapper | undefined;
afterEach(() => { view?.unmount(); view = undefined; vi.restoreAllMocks(); });
function render(axis: 'x' | 'y', value = 200, percent = false) {
  view = mount(ResizeHandle, { props: { axis, modelValue: value, percent, min: 20, max: 400, label: 'Resize panels' } });
  return view;
}
function pointer(target: EventTarget, type: string, values: Record<string, number>) {
  const event = new Event(type); Object.assign(event, { button: 0, pointerId: 1, clientX: 0, clientY: 0 }, values); target.dispatchEvent(event);
}
it.each(['x', 'y'] as const)('resizes on %s, ignores other pointers and releases listeners', async axis => {
  const page = render(axis); const coordinate = axis === 'x' ? 'clientX' : 'clientY';
  pointer(page.element, 'pointerdown', { button: 2 }); pointer(window, 'pointermove', { [coordinate]: 30 }); expect(page.emitted('update:modelValue')).toBeUndefined();
  pointer(page.element, 'pointerdown', { [coordinate]: 10 });
  pointer(window, 'pointermove', { pointerId: 2, [coordinate]: 60 }); expect(page.emitted('update:modelValue')).toBeUndefined();
  pointer(window, 'pointermove', { [coordinate]: 60 }); expect(page.emitted('update:modelValue')!.at(-1)).toEqual([250]);
  pointer(window, 'pointermove', { [coordinate]: 1000 }); expect(page.emitted('update:modelValue')!.at(-1)).toEqual([400]);
  pointer(window, 'pointermove', { [coordinate]: -1000 }); expect(page.emitted('update:modelValue')!.at(-1)).toEqual([20]);
  pointer(window, 'pointerup', {}); const count = page.emitted('update:modelValue')!.length;
  pointer(window, 'pointermove', { [coordinate]: 40 }); expect(page.emitted('update:modelValue')).toHaveLength(count);
  pointer(page.element, 'pointerdown', {}); pointer(window, 'pointercancel', {});
  expect(page.attributes('aria-orientation')).toBe(axis === 'x' ? 'vertical' : 'horizontal');
  expect(page.attributes('aria-label')).toBe('Resize panels');
});
it.each(['x', 'y'] as const)('supports percent geometry on %s, zero-sized containers and automatic initial sizes', axis => {
  const page = render(axis, 0, true); const parent = document.createElement('div'); const previous = document.createElement('section');
  parent.append(previous, page.element);
  vi.spyOn(parent, 'getBoundingClientRect').mockReturnValue({ width: 1000, height: 1000 } as DOMRect);
  vi.spyOn(previous, 'getBoundingClientRect').mockReturnValue({ width: 300, height: 300 } as DOMRect);
  pointer(page.element, 'pointerdown', {}); pointer(window, 'pointermove', { [axis === 'x' ? 'clientX' : 'clientY']: 100 });
  expect(page.emitted('update:modelValue')!.at(-1)).toEqual([40]);
  vi.mocked(parent.getBoundingClientRect).mockReturnValue({ width: 0, height: 0 } as DOMRect);
  pointer(page.element, 'pointerdown', {}); pointer(window, 'pointermove', { clientX: 1, clientY: 1 });
  expect(page.emitted('update:modelValue')!.at(-1)).toEqual([400]);
  page.unmount(); view = undefined; pointer(window, 'pointermove', {});
});
it.each(['x', 'y'] as const)('supports keyboard bounds and increments on %s', async axis => {
  const page = render(axis); const positive = axis === 'x' ? 'ArrowRight' : 'ArrowDown'; const negative = axis === 'x' ? 'ArrowLeft' : 'ArrowUp';
  await page.trigger('keydown', { key: 'a' }); expect(page.emitted('update:modelValue')).toBeUndefined();
  await page.trigger('keydown', { key: positive }); expect(page.emitted('update:modelValue')!.at(-1)).toEqual([216]);
  await page.trigger('keydown', { key: negative }); expect(page.emitted('update:modelValue')!.at(-1)).toEqual([184]);
  await page.trigger('keydown', { key: 'Home' }); expect(page.emitted('update:modelValue')!.at(-1)).toEqual([20]);
  await page.trigger('keydown', { key: 'End' }); expect(page.emitted('update:modelValue')!.at(-1)).toEqual([400]);
  await page.setProps({ percent: true }); await page.trigger('keydown', { key: positive }); expect(page.emitted('update:modelValue')!.at(-1)).toEqual([202]);
});
it('uses the measured size for keyboard resizing when no saved height exists', async () => {
  const page = render('y', 0); const parent = document.createElement('div'); const previous = document.createElement('section'); parent.append(previous, page.element);
  vi.spyOn(previous, 'getBoundingClientRect').mockReturnValue({ height: 300 } as DOMRect);
  await page.trigger('keydown', { key: 'ArrowDown' }); expect(page.emitted('update:modelValue')!.at(-1)).toEqual([316]);
});
