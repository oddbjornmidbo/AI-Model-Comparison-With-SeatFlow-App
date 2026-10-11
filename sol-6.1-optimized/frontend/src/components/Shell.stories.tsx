import type { Meta, StoryObj } from '@storybook/react';
import { expect, fn, userEvent, within } from '@storybook/test';
import { Shell } from './Shell';
import { StatCard } from './StatCard';
import { WorkshopCard } from './WorkshopCard';
import { workshops } from './storyFixtures';
const meta = {
  title: 'Conference/Application shell', component: Shell, tags: ['autodocs'], parameters: { layout: 'fullscreen' },
  args: { activePage: 'workshops', onPageChange: fn(), children: <><div className="page-heading"><div><span className="eyebrow">YOUR CONFERENCE, CONNECTED</span><h1>Workshops</h1><p>Great sessions. The right people. Every seat accounted for.</p></div><button type="button" className="button button-primary">New workshop</button></div><div className="stats-grid"><StatCard label="Workshops" value={4} icon="workshops" description="Ideas worth coming together for" /><StatCard label="Confirmed seats" value={20} icon="confirmed" description="18 seats still available" /><StatCard label="On the waitlist" value={2} icon="seats" description="Next in line for a great session" /><StatCard label="Participants" value={5} icon="participants" description="4 with an active registration" /></div><div className="workshop-grid">{workshops.map(workshop => <WorkshopCard key={workshop.id} workshop={workshop} onSelect={fn()} />)}</div></> },
} satisfies Meta<typeof Shell>;
export default meta;
type Story = StoryObj<typeof meta>;
export const Desktop: Story = { play: async ({ canvasElement, args }) => { await userEvent.click(within(canvasElement).getByRole('button', { name: 'Participants' })); await expect(args.onPageChange).toHaveBeenCalledWith('participants'); } };
export const ParticipantNavigation: Story = { args: { activePage: 'participants' } };
export const Tablet: Story = { parameters: { viewport: { defaultViewport: 'tablet' } } };
export const Mobile: Story = { parameters: { viewport: { defaultViewport: 'mobile1' } } };
export const Dark: Story = { globals: { theme: 'dark' }, parameters: { backgrounds: { default: 'Dark canvas' } } };
