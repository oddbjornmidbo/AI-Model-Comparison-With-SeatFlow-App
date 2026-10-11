import type { Meta, StoryObj } from '@storybook/react';
import { fireEvent } from '@testing-library/react';
import { expect, fn, userEvent, within } from '@storybook/test';
import { ParticipantSchedule } from './ParticipantSchedule';
import { overnightWorkshop, participants, registration, workshops, yearBoundaryWorkshop } from './storyFixtures';
const meta = { title: 'Participants/Schedule', component: ParticipantSchedule, tags: ['autodocs'], args: { participant: participants[0], registrations: [registration(1, participants[0], workshops[0], 'Confirmed'), registration(2, participants[0], workshops[1], 'Waitlisted'), registration(3, participants[0], workshops[3], 'Cancelled')], onClose: fn() } } satisfies Meta<typeof ParticipantSchedule>;
export default meta;
type Story = StoryObj<typeof meta>;
export const Populated: Story = { play: async ({ canvasElement, args }) => { const canvas = within(canvasElement); await expect(canvas.getByRole('dialog', { name: 'Sofia Berg' })).toBeVisible(); await userEvent.click(canvas.getByRole('button', { name: 'Done' })); await expect(args.onClose).toHaveBeenCalled(); } };
export const Empty: Story = { args: { registrations: [] } };
export const AcrossDaysAndYears: Story = {
  tags: ['cross-day'],
  args: {
    registrations: [
      registration(1, participants[0], overnightWorkshop, 'Confirmed'),
      registration(2, participants[0], yearBoundaryWorkshop, 'Waitlisted'),
    ],
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getByText('15 Oct, 23:00 – 16 Oct, 01:00')).toBeVisible();
    await expect(canvas.getByText('31 Dec 2026, 23:00 – 1 Jan 2027, 01:00')).toBeVisible();
  },
};
export const Loading: Story = { args: { registrations: null, loading: true } };
export const Failed: Story = { args: { registrations: null, error: 'The latest schedule could not be loaded. Close this window and refresh the workspace.' } };
export const StaleAfterRefreshFailure: Story = { args: { error: 'The displayed schedule may be out of date. Refresh the workspace to see the latest registrations.' } };
// Synthetic userEvent keypresses do not invoke the browser's native dialog
// cancellation. The real Escape workflow is covered by application E2E tests.
export const NativeCancellation: Story = { play: async ({ canvasElement, args }) => { fireEvent(within(canvasElement).getByRole('dialog'), new Event('cancel', { cancelable: true })); await expect(args.onClose).toHaveBeenCalled(); } };
export const Mobile: Story = { parameters: { viewport: { defaultViewport: 'mobile1' } } };
export const Dark: Story = { globals: { theme: 'dark' }, parameters: { backgrounds: { default: 'Dark canvas' } } };
