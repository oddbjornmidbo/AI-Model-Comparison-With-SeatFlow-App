import type { Meta, StoryObj } from '@storybook/react';
import { expect, userEvent, waitFor, within } from '@storybook/test';
import { App } from './App';
import type { Participant, Registration, ScheduleItem, SeatFlowClient, Workshop } from './types';

const workshops: Workshop[] = [
  { id: 'w-1', title: 'Designing for the real world', startTime: '2030-04-16T09:00:00Z', endTime: '2030-04-16T10:00:00Z', capacity: 2, confirmedCount: 0, remainingCapacity: 2 },
  { id: 'w-2', title: 'Stories with data', startTime: '2030-04-16T09:30:00Z', endTime: '2030-04-16T10:30:00Z', capacity: 1, confirmedCount: 1, remainingCapacity: 0 },
];
const participants: Participant[] = [
  { id: 'p-1', name: 'Alex Morgan', email: 'alex@example.com' },
  { id: 'p-2', name: 'Jamie Chen', email: 'jamie@example.com' },
];

interface DemoClientOptions {
  firstWorkshopId?: string;
  firstParticipantId?: string;
  failRegistration?: boolean;
  failWorkshopRefresh?: boolean;
  failRegistrationListRefresh?: boolean;
  promotionScenario?: boolean;
}

function createDemoClient(options: DemoClientOptions = {}): SeatFlowClient {
  const demoParticipants = options.promotionScenario
    ? [...participants, { id: 'p-3', name: 'Sam Rivera', email: 'sam@example.com' }, { id: 'p-4', name: 'Taylor Brooks', email: 'taylor@example.com' }]
    : participants;
  const registrations: Registration[] = options.promotionScenario ? [
    { id: 'r-1', workshopId: 'w-1', participantId: 'p-1', participantName: 'Alex Morgan', participantEmail: 'alex@example.com', status: 'Confirmed', createdAt: '2030-04-01T08:00:00Z' },
    { id: 'r-2', workshopId: 'w-2', participantId: 'p-3', participantName: 'Sam Rivera', participantEmail: 'sam@example.com', status: 'Confirmed', createdAt: '2030-04-01T09:00:00Z' },
    { id: 'r-3', workshopId: 'w-2', participantId: 'p-1', participantName: 'Alex Morgan', participantEmail: 'alex@example.com', status: 'Waitlisted', createdAt: '2030-04-01T09:01:00Z' },
    { id: 'r-4', workshopId: 'w-2', participantId: 'p-4', participantName: 'Taylor Brooks', participantEmail: 'taylor@example.com', status: 'Waitlisted', createdAt: '2030-04-01T09:02:00Z' },
  ] : [{ id: 'r-2', workshopId: 'w-2', participantId: 'p-2', participantName: 'Jamie Chen', participantEmail: 'jamie@example.com', status: 'Confirmed', createdAt: '2030-04-01T09:00:00Z' }];
  const schedule: ScheduleItem[] = options.promotionScenario ? [
    { registrationId: 'r-1', workshopId: 'w-1', workshopTitle: 'Designing for the real world', startTime: '2030-04-16T09:00:00Z', endTime: '2030-04-16T10:00:00Z', status: 'Confirmed' },
    { registrationId: 'r-2', workshopId: 'w-2', workshopTitle: 'Stories with data', startTime: '2030-04-16T09:30:00Z', endTime: '2030-04-16T10:30:00Z', status: 'Confirmed' },
  ] : [{ registrationId: 'r-2', workshopId: 'w-2', workshopTitle: 'Stories with data', startTime: '2030-04-16T09:30:00Z', endTime: '2030-04-16T10:30:00Z', status: 'Confirmed' }];
  const idempotency = new Map<string, Registration>();
  let workshopReads = 0;
  let registrationReads = 0;
  const counts = () => workshops.map((item) => {
    const confirmedCount = registrations.filter((registration) => registration.workshopId === item.id && registration.status === 'Confirmed').length;
    return { ...item, confirmedCount, remainingCapacity: Math.max(0, item.capacity - confirmedCount) };
  });
  return {
    getWorkshops: async () => {
      workshopReads += 1;
      if (options.failWorkshopRefresh && workshopReads > 1) throw new Error('Workshop service is unavailable.');
      const result = counts();
      if (options.firstWorkshopId) result.sort((a, b) => Number(b.id === options.firstWorkshopId) - Number(a.id === options.firstWorkshopId));
      return result;
    },
    getParticipants: async () => options.firstParticipantId
      ? [...demoParticipants].sort((a, b) => Number(b.id === options.firstParticipantId) - Number(a.id === options.firstParticipantId))
      : demoParticipants,
    getRegistrations: async (workshopId) => {
      registrationReads += 1;
      if (options.failRegistrationListRefresh && registrationReads > 1) throw new Error('Attendee list is temporarily unavailable.');
      return registrations.filter((item) => item.workshopId === workshopId).map((item) => ({ ...item }));
    },
    getSchedule: async (participantId) => schedule.filter((item) => registrations.some((reg) => reg.id === item.registrationId && reg.participantId === participantId && reg.status === 'Confirmed')).map((item) => ({ ...item })),
    register: async (workshopId, participantId, key) => {
      if (options.failRegistration) throw new Error('Registration could not be saved. Please try again.');
      const previous = idempotency.get(key);
      if (previous) return previous;
      const participant = demoParticipants.find((item) => item.id === participantId)!;
      const workshop = counts().find((item) => item.id === workshopId)!;
      const item: Registration = { id: `r-${Date.now()}`, workshopId, participantId, participantName: participant.name, participantEmail: participant.email, status: workshop.remainingCapacity > 0 ? 'Confirmed' : 'Waitlisted', createdAt: new Date().toISOString() };
      registrations.push(item);
      idempotency.set(key, item);
      if (item.status === 'Confirmed') schedule.push({ registrationId: item.id, workshopId, workshopTitle: workshop.title, startTime: workshop.startTime, endTime: workshop.endTime, status: 'Confirmed' });
      return item;
    },
    cancel: async (id) => {
      const item = registrations.find((registration) => registration.id === id)!;
      const wasConfirmed = item.status === 'Confirmed';
      item.status = 'Cancelled';
      if (wasConfirmed) {
        const workshop = workshops.find((candidate) => candidate.id === item.workshopId)!;
        const confirmedSchedule = schedule.filter((session) => registrations.some((registration) => registration.id === session.registrationId && registration.status === 'Confirmed'));
        const waitlisted = registrations.filter((registration) => registration.workshopId === item.workshopId && registration.status === 'Waitlisted')
          .sort((a, b) => a.createdAt.localeCompare(b.createdAt) || a.id.localeCompare(b.id));
        const promoted = waitlisted.find((candidate) => !confirmedSchedule.some((session) => {
          const scheduledRegistration = registrations.find((registration) => registration.id === session.registrationId);
          return scheduledRegistration?.participantId === candidate.participantId
            && new Date(workshop.startTime) < new Date(session.endTime)
            && new Date(workshop.endTime) > new Date(session.startTime);
        }));
        if (promoted) {
          promoted.status = 'Confirmed';
          schedule.push({ registrationId: promoted.id, workshopId: workshop.id, workshopTitle: workshop.title, startTime: workshop.startTime, endTime: workshop.endTime, status: 'Confirmed' });
        }
      }
      return { ...item };
    },
  };
}

const meta = { title: 'SeatFlow/Application', component: App, parameters: { layout: 'fullscreen' }, tags: ['autodocs'] } satisfies Meta<typeof App>;
export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = { args: { client: createDemoClient() } };
export const RegistrationFlow: Story = {
  args: { client: createDemoClient() },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const register = await canvas.findByRole('button', { name: /register/i });
    await waitFor(() => expect(register).toBeEnabled());
    await userEvent.click(register);
    await expect(await canvas.findByText(/Alex Morgan is confirmed for Designing for the real world/i)).toBeInTheDocument();
    const confirmedGroup = canvas.getByRole('region', { name: /confirmed registrations/i });
    await expect(within(confirmedGroup).getByText('Alex Morgan')).toBeInTheDocument();
  },
};
export const Mobile: Story = { args: { client: createDemoClient() }, parameters: { viewport: { defaultViewport: 'mobile1' } } };
export const Dark: Story = { args: { client: createDemoClient() }, parameters: { colorScheme: 'dark' } };
export const FullWorkshopWaitlist: Story = {
  args: { client: createDemoClient({ firstWorkshopId: 'w-2' }) },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const join = await canvas.findByRole('button', { name: /join waitlist/i });
    await waitFor(() => expect(join).toBeEnabled());
    await userEvent.click(join);
    await expect(await canvas.findByText(/Alex Morgan is added to the waitlist for Stories with data/i)).toBeInTheDocument();
    await expect(canvas.getByRole('region', { name: /waitlist registrations/i })).toHaveTextContent('Alex Morgan');
  },
};
export const AlreadyRegistered: Story = {
  args: { client: createDemoClient({ firstWorkshopId: 'w-2', firstParticipantId: 'p-2' }) },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const register = await canvas.findByRole('button', { name: /join waitlist/i });
    await waitFor(() => expect(register).toBeDisabled());
    await expect(canvas.getByText(/Jamie Chen already has an active registration for this workshop/i)).toBeInTheDocument();
  },
};
export const ScheduleConflict: Story = {
  args: { client: createDemoClient({ firstParticipantId: 'p-2' }) },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const register = await canvas.findByRole('button', { name: /register/i });
    await waitFor(() => expect(register).toBeDisabled());
    await expect(canvas.getByText(/overlaps a confirmed session on this participant’s schedule/i)).toBeInTheDocument();
  },
};
export const ActionFailure: Story = {
  args: { client: createDemoClient({ failRegistration: true }) },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const register = await canvas.findByRole('button', { name: /register/i });
    await waitFor(() => expect(register).toBeEnabled());
    await userEvent.click(register);
    await expect(await canvas.findByRole('alert')).toHaveTextContent('Registration could not be saved. Please try again.');
  },
};
export const RefreshFailure: Story = {
  args: { client: createDemoClient({ failWorkshopRefresh: true }) },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await canvas.findByRole('button', { name: /register/i });
    await userEvent.click(canvas.getByRole('button', { name: /refresh/i }));
    await expect(await canvas.findByText('Workshop service is unavailable.')).toBeInTheDocument();
    await expect(canvas.findByText(/some information could not be refreshed/i)).resolves.toBeInTheDocument();
  },
};
export const CancellationPromotion: Story = {
  args: { client: createDemoClient({ firstWorkshopId: 'w-2', promotionScenario: true }) },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(await canvas.findByRole('button', { name: /cancel Sam Rivera's confirmed registration/i }));
    await expect(await canvas.findByText(/Sam Rivera's registration was cancelled/i)).toBeInTheDocument();
    const confirmedGroup = canvas.getByRole('region', { name: /confirmed registrations/i });
    const waitlistGroup = canvas.getByRole('region', { name: /waitlist registrations/i });
    await expect(within(confirmedGroup).getByText('Taylor Brooks')).toBeInTheDocument();
    await expect(within(waitlistGroup).getByText('Alex Morgan')).toBeInTheDocument();
    await expect(within(waitlistGroup).queryByText('Taylor Brooks')).not.toBeInTheDocument();
    await userEvent.selectOptions(canvas.getByLabelText('Participant'), 'p-4');
    const schedule = canvas.getByRole('region', { name: /participant schedule/i });
    await expect(await within(schedule).findByText('Stories with data')).toBeInTheDocument();
  },
};
export const SavedWaitlistWithRefreshFailure: Story = {
  args: { client: createDemoClient({ firstWorkshopId: 'w-2', failRegistrationListRefresh: true }) },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const join = await canvas.findByRole('button', { name: /join waitlist/i });
    await waitFor(() => expect(join).toBeEnabled());
    await userEvent.click(join);
    await expect(await canvas.findByText(/registration was saved, but one or more views could not refresh/i)).toBeInTheDocument();
    await expect(await canvas.findByText('Attendee list is temporarily unavailable.')).toBeInTheDocument();
    await waitFor(() => expect(join).toBeDisabled());
    await expect(canvas.getByText(/Alex Morgan already has an active registration for this workshop/i)).toBeInTheDocument();
  },
};
export const Loading: Story = {
  args: { client: { ...createDemoClient(), getWorkshops: () => new Promise(() => {}), getParticipants: () => new Promise(() => {}) } },
};
export const ErrorState: Story = {
  args: { client: { ...createDemoClient(), getWorkshops: async () => { throw new Error('Workshop service is unavailable.'); } } },
};
