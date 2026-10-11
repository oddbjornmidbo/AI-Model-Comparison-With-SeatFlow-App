import type { Meta, StoryObj } from '@storybook/react';
import { RegistrationList } from './RegistrationList';
import type { Registration } from '../types';

const registrations: Registration[] = [
  { id: 'r-1', workshopId: 'w-1', participantId: 'p-1', participantName: 'Alex Morgan', participantEmail: 'alex@example.com', status: 'Confirmed', createdAt: '2030-04-01T09:00:00Z' },
  { id: 'r-2', workshopId: 'w-1', participantId: 'p-2', participantName: 'Jamie Chen', participantEmail: 'jamie@example.com', status: 'Waitlisted', createdAt: '2030-04-01T09:01:00Z' },
  { id: 'r-3', workshopId: 'w-1', participantId: 'p-3', participantName: 'Sam Rivera', participantEmail: 'sam@example.com', status: 'Cancelled', createdAt: '2030-04-01T09:02:00Z' },
];
const meta = { title: 'SeatFlow/Registration list', component: RegistrationList, args: { registrations }, tags: ['autodocs'] } satisfies Meta<typeof RegistrationList>;
export default meta;
type Story = StoryObj<typeof meta>;

export const Normal: Story = {};
export const Empty: Story = { args: { registrations: [] } };
export const Loading: Story = { args: { registrations: [], loading: true } };
export const Error: Story = { args: { registrations: [], error: 'Could not load registrations. Try again.' } };
export const Cancelling: Story = { args: { busyRegistrationId: 'r-1' } };
export const Narrow: Story = { parameters: { viewport: { defaultViewport: 'mobile1' } } };
