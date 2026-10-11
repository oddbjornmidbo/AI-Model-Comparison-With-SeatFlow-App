import type { Meta, StoryObj } from '@storybook/react';
import { Loading } from './Feedback';
const meta = { title: 'Feedback/Loading', component: Loading, tags: ['autodocs'], args: { label: 'Getting your conference ready…' } } satisfies Meta<typeof Loading>;
export default meta;
type Story = StoryObj<typeof meta>;
export const Workshops: Story = {};
export const Schedule: Story = { args: { label: 'Loading participant schedule…' } };
export const Dark: Story = { globals: { theme: 'dark' } };
