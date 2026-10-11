import type { Meta, StoryObj } from '@storybook/react';
import { expect, userEvent, within } from '@storybook/test';
import { WorkshopCard } from './WorkshopCard';
import type { Workshop } from '../types';

const sample: Workshop = { id: 'workshop-a', title: 'Designing for the real world', startTime: '2030-04-16T09:00:00Z', endTime: '2030-04-16T10:00:00Z', capacity: 12, confirmedCount: 8, remainingCapacity: 4 };
const meta = { title: 'SeatFlow/Workshop card', component: WorkshopCard, args: { workshop: sample }, tags: ['autodocs'] } satisfies Meta<typeof WorkshopCard>;
export default meta;
type Story = StoryObj<typeof meta>;

export const Available: Story = {};
export const Selected: Story = { args: { selected: true } };
export const Full: Story = { args: { workshop: { ...sample, capacity: 8, confirmedCount: 8, remainingCapacity: 0 } } };
export const Narrow: Story = { parameters: { viewport: { defaultViewport: 'mobile1' } } };
export const KeyboardSelection: Story = {
  args: { onSelect: (id) => { document.body.dataset.selectedWorkshop = id; } },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const card = canvas.getByRole('button', { name: /designing for the real world/i });
    card.focus();
    await userEvent.keyboard('{Enter}');
    await expect(document.body.dataset.selectedWorkshop).toBe('workshop-a');
  },
};
