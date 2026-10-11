import type { Meta, StoryObj } from '@storybook/react';
import { expect, fn, userEvent, within } from '@storybook/test';
import { WorkshopCard } from './WorkshopCard';
import { overnightWorkshop, workshops, yearBoundaryWorkshop } from './storyFixtures';
const meta = { title: 'Workshops/Workshop card', component: WorkshopCard, tags: ['autodocs'], args: { workshop: workshops[0], onSelect: fn() }, decorators: [Story => <div style={{ maxWidth: 350 }}><Story /></div>] } satisfies Meta<typeof WorkshopCard>;
export default meta;
type Story = StoryObj<typeof meta>;
export const Available: Story = { play: async ({ canvasElement, args }) => { const button = within(canvasElement).getByRole('button', { name: /building thoughtful/i }); await userEvent.click(button); await expect(args.onSelect).toHaveBeenCalledWith(workshops[0]); } };
export const Selected: Story = { args: { selected: true }, play: async ({ canvasElement }) => { await expect(within(canvasElement).getByRole('button')).toHaveAttribute('aria-pressed', 'true'); } };
export const Full: Story = { args: { workshop: workshops[1] } };
export const AlmostFull: Story = { args: { workshop: workshops[2] } };
export const Overnight: Story = {
  tags: ['cross-day'],
  args: { workshop: overnightWorkshop },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getByText('15 – 16 Oct 2026')).toBeVisible();
    await expect(canvas.getByText('15 Oct, 23:00 – 16 Oct, 01:00')).toBeVisible();
    await expect(canvas.getByRole('button')).toHaveAccessibleName(/15 Oct 2026 at 23:00 to 16 Oct 2026 at 01:00/);
  },
};
export const AcrossYears: Story = {
  tags: ['cross-day'],
  args: { workshop: yearBoundaryWorkshop },
  play: async ({ canvasElement }) => {
    await expect(within(canvasElement).getByText('31 Dec 2026, 23:00 – 1 Jan 2027, 01:00')).toBeVisible();
  },
};
export const Mobile: Story = { parameters: { viewport: { defaultViewport: 'mobile1' } } };
export const Dark: Story = { globals: { theme: 'dark' }, parameters: { backgrounds: { default: 'Dark canvas' } } };
