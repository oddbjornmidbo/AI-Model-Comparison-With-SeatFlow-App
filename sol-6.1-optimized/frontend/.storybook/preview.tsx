import type { Preview } from '@storybook/react';
import '../src/styles.css';
const preview: Preview = {
  parameters: {
    controls: { matchers: { color: /(background|color)$/i, date: /Date$/i } },
    layout: 'padded',
    backgrounds: { default: 'Conference canvas', values: [{ name: 'Conference canvas', value: '#f7f8fb' }, { name: 'Dark canvas', value: '#151821' }] },
  },
  globalTypes: {
    theme: { description: 'Color theme', toolbar: { title: 'Theme', icon: 'circlehollow', items: [{ value: 'light', title: 'Light' }, { value: 'dark', title: 'Dark' }], dynamicTitle: true } },
  },
  initialGlobals: { theme: 'light' },
  decorators: [(Story, context) => <div data-theme={context.globals.theme} style={{ minHeight: '100%', color: 'var(--ink)', background: 'var(--page)' }}><Story /></div>],
};
export default preview;
