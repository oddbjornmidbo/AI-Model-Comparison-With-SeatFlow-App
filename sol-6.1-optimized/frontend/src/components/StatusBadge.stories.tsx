import type { Meta, StoryObj } from '@storybook/react';
import { StatusBadge } from './StatusBadge';
const meta = { title: 'Feedback/Registration status', component: StatusBadge, tags: ['autodocs'], args: { status: 'Confirmed' } } satisfies Meta<typeof StatusBadge>;
export default meta;
type Story = StoryObj<typeof meta>;
export const Confirmed: Story = {};
export const Waitlisted: Story = { args: { status: 'Waitlisted' } };
export const Cancelled: Story = { args: { status: 'Cancelled' } };
export const Dark: Story = { globals: { theme: 'dark' } };
