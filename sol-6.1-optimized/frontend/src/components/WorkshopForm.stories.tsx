import type { Meta, StoryObj } from '@storybook/react';
import { fireEvent } from '@testing-library/react';
import { expect, fn, userEvent, within } from '@storybook/test';
import { WorkshopForm } from './WorkshopForm';
const meta = { title: 'Workshops/Create workshop', component: WorkshopForm, tags: ['autodocs'], args: { open: true, onClose: fn(), onSubmit: fn() } } satisfies Meta<typeof WorkshopForm>;
export default meta;
type Story = StoryObj<typeof meta>;
export const Default: Story = {};
export const Create: Story = {
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    await userEvent.type(canvas.getByRole('textbox', { name: 'Workshop title' }), 'Design systems that scale');
    fireEvent.change(canvas.getByLabelText('Starts'), { target: { value: '2026-10-12T09:00' } });
    fireEvent.change(canvas.getByLabelText('Ends'), { target: { value: '2026-10-12T10:30' } });
    await userEvent.clear(canvas.getByRole('spinbutton', { name: 'Seat capacity' }));
    await userEvent.type(canvas.getByRole('spinbutton', { name: 'Seat capacity' }), '16');
    await userEvent.click(canvas.getByRole('button', { name: 'Create workshop' }));
    await expect(args.onSubmit).toHaveBeenCalledWith({ title: 'Design systems that scale', start: new Date('2026-10-12T09:00').toISOString(), end: new Date('2026-10-12T10:30').toISOString(), capacity: 16 });
  },
};
export const Validation: Story = {
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    await userEvent.type(canvas.getByRole('textbox', { name: 'Workshop title' }), 'A thoughtful workshop');
    fireEvent.change(canvas.getByLabelText('Starts'), { target: { value: '2026-10-12T11:00' } });
    fireEvent.change(canvas.getByLabelText('Ends'), { target: { value: '2026-10-12T10:00' } });
    await userEvent.click(canvas.getByRole('button', { name: 'Create workshop' }));
    await expect(canvas.getByRole('alert')).toHaveTextContent('The end time must be after the start time.');
    await expect(args.onSubmit).not.toHaveBeenCalled();
  },
};
export const Error: Story = { args: { error: 'The workshop could not be saved. Check the connection and try again.' } };
export const Saving: Story = { args: { busy: true }, play: async ({ canvasElement }) => { await expect(within(canvasElement).getByRole('button', { name: 'Creating workshop…' })).toBeDisabled(); } };
export const Cancel: Story = { play: async ({ canvasElement, args }) => { await userEvent.click(within(canvasElement).getByRole('button', { name: 'Cancel' })); await expect(args.onClose).toHaveBeenCalled(); } };
export const Mobile: Story = { parameters: { viewport: { defaultViewport: 'mobile1' } } };
export const Dark: Story = { globals: { theme: 'dark' }, parameters: { backgrounds: { default: 'Dark canvas' } } };
export const RecoverLostResponse: Story = {
  args: {
    pendingInput: { title: 'Design systems that scale', start: '2026-10-12T09:00:00Z', end: '2026-10-12T10:30:00Z', capacity: 16 },
  },
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getByRole('alert')).toHaveTextContent('A previous creation still needs checking');
    await expect(canvas.getByRole('textbox', { name: 'Workshop title' })).toBeDisabled();
    await expect(canvasElement.ownerDocument.activeElement).toBe(canvas.getByRole('button', { name: 'Retry workshop creation' }));
    await userEvent.click(canvas.getByRole('button', { name: 'Retry workshop creation' }));
    await expect(args.onSubmit).toHaveBeenCalledWith(args.pendingInput);
  },
};
