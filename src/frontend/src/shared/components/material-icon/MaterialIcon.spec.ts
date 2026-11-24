import { expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import MaterialIcon from './MaterialIcon.vue';

it('renders a decorative icon without adding its technical name to visible or accessible text', async () => {
  const view = mount(MaterialIcon, { props: { name: 'account_tree' } });
  expect(view.attributes('aria-hidden')).toBe('true');
  expect(view.attributes('data-icon')).toBe('account_tree');
  expect(view.text()).toBe('');
  await view.setProps({ name: 'subdirectory_arrow_right' });
  expect(view.attributes('data-icon')).toBe('subdirectory_arrow_right');
  view.unmount();
});
