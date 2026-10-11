import type { Meta, StoryObj } from '@storybook/react';
import { StatusBadge } from './StatusBadge';

const meta = { title: 'SeatFlow/Status badge', component: StatusBadge, tags: ['autodocs'] } satisfies Meta<typeof StatusBadge>;
export default meta;
type Story = StoryObj<typeof meta>;

export const Confirmed: Story = { args: { status: 'Confirmed' } };
export const Waitlisted: Story = { args: { status: 'Waitlisted' } };
export const Cancelled: Story = { args: { status: 'Cancelled' } };
