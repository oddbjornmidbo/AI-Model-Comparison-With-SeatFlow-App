import type { Meta, StoryObj } from '@storybook/react';
import { StatCard } from './StatCard';
const meta = { title: 'Conference/Overview statistic', component: StatCard, tags: ['autodocs'], args: { label: 'Confirmed seats', value: 20, icon: 'confirmed', description: '18 seats still available' }, decorators: [Story => <div style={{ maxWidth: 270 }}><Story /></div>] } satisfies Meta<typeof StatCard>;
export default meta;
type Story = StoryObj<typeof meta>;
export const Populated: Story = {};
export const Empty: Story = { args: { value: 0, description: 'A new conference, ready to grow' } };
export const Participants: Story = { args: { label: 'Participants', value: 42, icon: 'participants', description: 'A room full of possibilities' } };
export const Dark: Story = { globals: { theme: 'dark' } };
