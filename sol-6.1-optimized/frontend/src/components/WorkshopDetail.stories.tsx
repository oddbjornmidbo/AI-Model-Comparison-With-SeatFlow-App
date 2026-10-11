import type { Meta, StoryObj } from '@storybook/react';
import { expect, fn, userEvent, within } from '@storybook/test';
import { WorkshopDetail } from './WorkshopDetail';
import { overnightWorkshop, participants, registrations, workshops } from './storyFixtures';
const meta = {
  title: 'Workshops/Workshop details', component: WorkshopDetail, tags: ['autodocs'],
  args: { workshop: workshops[1], registrations, participants, selectedParticipantId: 5, onParticipantChange: fn(), onRegister: fn(), onCancel: fn(), onViewSchedule: fn() },
  decorators: [Story => <div style={{ maxWidth: 430 }}><Story /></div>],
} satisfies Meta<typeof WorkshopDetail>;
export default meta;
type Story = StoryObj<typeof meta>;
export const Populated: Story = {
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: 'View Sofia Berg\'s schedule' }));
    await expect(args.onViewSchedule).toHaveBeenCalledWith(participants[0]);
    await userEvent.click(canvas.getByRole('button', { name: 'Cancel registration for Amara Okafor' }));
    await expect(args.onCancel).toHaveBeenCalledWith(registrations[2]);
  },
};
export const Available: Story = {
  args: { workshop: workshops[0], registrations: [], registrationHint: 'A seat is available. Ella will be confirmed immediately.' },
  play: async ({ canvasElement, args }) => { await userEvent.click(within(canvasElement).getByRole('button', { name: 'Register participant' })); await expect(args.onRegister).toHaveBeenCalled(); },
};
export const Overnight: Story = {
  tags: ['cross-day'],
  args: { workshop: overnightWorkshop, registrations: [] },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getByText('15 – 16 Oct 2026')).toBeVisible();
    await expect(canvas.getByText('15 Oct, 23:00 – 16 Oct, 01:00')).toBeVisible();
  },
};
export const Full: Story = {
  registrationHint: undefined,
  play: async ({ canvasElement, args }) => { await userEvent.click(within(canvasElement).getByRole('button', { name: 'Add to waitlist' })); await expect(args.onRegister).toHaveBeenCalled(); },
} as Story;
export const Conflict: Story = { args: { workshop: workshops[0], registrations: [], registrationDisabled: true, registrationHint: 'Ella has a confirmed workshop that overlaps this session. Choose another participant.' }, play: async ({ canvasElement }) => { await expect(within(canvasElement).getByRole('button', { name: 'Register participant' })).toBeDisabled(); await expect(within(canvasElement).getByText(/Ella has a confirmed workshop/)).toBeVisible(); } };
export const AlreadyRegistered: Story = { args: { selectedParticipantId: 1, registrationDisabled: true, registrationHint: 'Sofia already has a confirmed registration for this workshop.' }, play: async ({ canvasElement }) => { await expect(within(canvasElement).getByRole('option', { name: 'Sofia Berg — confirmed' })).toBeDisabled(); await expect(within(canvasElement).getByRole('button', { name: 'Add to waitlist' })).toBeDisabled(); } };
export const UncertainRequest: Story = { args: { pendingAttempt: true, registrationHint: 'The previous result is uncertain. Retry this same registration safely.' }, play: async ({ canvasElement }) => { await expect(within(canvasElement).getByRole('combobox', { name: 'Participant' })).toBeDisabled(); await expect(within(canvasElement).getByRole('button', { name: 'Retry registration' })).toBeEnabled(); } };
export const PendingAfterReload: Story = { args: { selectedParticipantId: null, pendingParticipantIds: [1], registrationDisabled: true, registrationHint: 'Choose the participant to resolve the previous registration attempt.' }, play: async ({ canvasElement, args }) => { const canvas = within(canvasElement); await expect(canvas.getByRole('option', { name: 'Sofia Berg — retry previous attempt' })).toBeEnabled(); await userEvent.selectOptions(canvas.getByRole('combobox', { name: 'Participant' }), '1'); await expect(args.onParticipantChange).toHaveBeenCalledWith(1); } };
export const LoadingRoster: Story = { args: { loading: true, registrations: [] } };
export const Updating: Story = { args: { busy: true } };
export const Error: Story = { args: { error: 'The registrations could not be loaded. Refresh the workspace and try again.', registrations: [] } };
export const CancelledHistory: Story = { play: async ({ canvasElement }) => { const canvas = within(canvasElement); await userEvent.click(canvas.getByText('Cancelled registrations')); await expect(canvas.getByText('Cancelled', { selector: '.status-badge' })).toBeVisible(); } };
export const Mobile: Story = { parameters: { viewport: { defaultViewport: 'mobile1' } } };
export const Dark: Story = { globals: { theme: 'dark' }, parameters: { backgrounds: { default: 'Dark canvas' } } };
