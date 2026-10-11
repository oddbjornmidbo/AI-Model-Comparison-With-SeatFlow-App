import type { Meta, StoryObj } from '@storybook/react';
import { ParticipantSchedule } from './ParticipantSchedule';
import type { Participant, ScheduleItem } from '../types';

const people: Participant[] = [{ id: 'p-1', name: 'Alex Morgan', email: 'alex@example.com' }, { id: 'p-2', name: 'Jamie Chen', email: 'jamie@example.com' }];
const schedule: ScheduleItem[] = [{ registrationId: 'r-1', workshopId: 'w-1', workshopTitle: 'Designing for the real world', startTime: '2030-04-16T09:00:00Z', endTime: '2030-04-16T10:00:00Z', status: 'Confirmed' }];
const meta = { title: 'SeatFlow/Participant schedule', component: ParticipantSchedule, args: { participants: people, selectedParticipantId: 'p-1', schedule, onParticipantChange: () => {} }, tags: ['autodocs'] } satisfies Meta<typeof ParticipantSchedule>;
export default meta;
type Story = StoryObj<typeof meta>;

export const WithSessions: Story = {};
export const Empty: Story = { args: { schedule: [] } };
export const Loading: Story = { args: { loading: true } };
export const Error: Story = { args: { schedule: [], error: 'Could not load schedule.' } };
export const Narrow: Story = { parameters: { viewport: { defaultViewport: 'mobile1' } } };
export const Dark: Story = { parameters: { colorScheme: 'dark' } };
