import type { Preview } from '@storybook/react';
import { createElement } from 'react';
import '../src/styles.css';

const preview: Preview = {
  decorators: [(Story, context) => {
    if (context.parameters.colorScheme === 'dark') document.documentElement.dataset.colorScheme = 'dark';
    else delete document.documentElement.dataset.colorScheme;
    return createElement(Story);
  }],
  parameters: {
    controls: { expanded: true },
    layout: 'centered',
    backgrounds: {
      options: {
        light: { name: 'Light', value: '#f6f5f1' },
        dark: { name: 'Dark', value: '#141917' },
      },
    },
    a11y: { test: 'error' },
  },
};

export default preview;
