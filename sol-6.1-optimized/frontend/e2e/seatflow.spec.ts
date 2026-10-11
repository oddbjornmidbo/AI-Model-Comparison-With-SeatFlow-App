import { test as base, expect, type APIRequestContext, type Page } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import type { Registration, Workshop } from '../src/types';

interface Program { available: Workshop; overlap: Workshop; touching: Workshop; }

const test = base.extend<{ program: Program }>({
  program: async ({ request }, use, info) => {
    const title = `${info.project.name} ${info.title}`;
    const create = async (suffix: string, start: string, end: string, capacity: number) => {
      const response = await request.post('/api/workshops', { data: { title: `${title} ${suffix}`, start, end, capacity } });
      expect(response.status()).toBe(201);
      return await response.json() as Workshop;
    };
    const program = {
      available: await create('A', '2030-06-12T10:00:00Z', '2030-06-12T11:00:00Z', 1),
      overlap: await create('B', '2030-06-12T10:30:00Z', '2030-06-12T11:30:00Z', 1),
      touching: await create('C', '2030-06-12T11:00:00Z', '2030-06-12T12:00:00Z', 2),
    };
    await use(program);
    for (const workshop of Object.values(program)) {
      const registrations = await (await request.get(`/api/workshops/${workshop.id}/registrations`)).json() as Registration[];
      for (const registration of registrations.filter(item => item.status !== 'Cancelled')) await request.post(`/api/registrations/${registration.id}/cancel`);
    }
  },
});

async function choose(page: Page, workshop: Workshop) {
  await page.getByLabel('Search workshops').fill(workshop.title);
  await page.getByRole('button', { name: new RegExp(`^${workshop.title.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')},`) }).click();
  await expect(page.getByRole('complementary', { name: 'Selected workshop' }).getByRole('heading', { level: 2 })).toHaveText(workshop.title);
}

const detail = (page: Page) => page.getByRole('complementary', { name: 'Selected workshop' });

async function register(page: Page, participantId: number) {
  await detail(page).getByLabel('Participant', { exact: true }).selectOption(String(participantId));
  await detail(page).getByRole('button', { name: /^(Register participant|Add to waitlist|Retry registration)$/ }).click();
  await expect(page.getByRole('button', { name: 'Refresh', exact: true })).toBeEnabled();
}

async function apiRegister(request: APIRequestContext, workshopId: number, participantId: number) {
  const response = await request.post(`/api/workshops/${workshopId}/registrations`, { data: { participantId }, headers: { 'Idempotency-Key': crypto.randomUUID() } });
  expect(response.status()).toBe(201);
  return await response.json() as Registration;
}

test('initial dashboard has no overflow or runtime errors', async ({ page }, info) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
  await page.goto('/');
  await expect(page.getByRole('button', { name: 'Refresh', exact: true })).toBeEnabled();
  await expect(page.getByRole('heading', { name: 'Find your next session' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await mkdir('../verification', { recursive: true });
  await page.screenshot({ path: `../verification/seatflow-${info.project.name}.png`, fullPage: true });
  expect(errors).toEqual([]);
});

test('registration, waitlisting, promotion and re-registration keep every view consistent', async ({ page, program }) => {
  await page.goto('/');
  await choose(page, program.available);
  await register(page, 1);
  await expect(page.getByText('Seat confirmed', { exact: true })).toBeVisible();
  await expect(detail(page).getByText('1 of 1 seats filled', { exact: true })).toBeVisible();
  await expect(detail(page).getByRole('button', { name: 'Add to waitlist', exact: true })).toBeDisabled();
  await expect(detail(page).getByText('Maya Chen already has a confirmed seat.')).toBeVisible();
  await register(page, 2);
  await expect(page.getByText('Added to the waitlist', { exact: true })).toBeVisible();
  await expect(detail(page).getByRole('button', { name: "View Oliver Berg's schedule" })).toBeVisible();
  await detail(page).getByRole('button', { name: 'Cancel registration for Maya Chen', exact: true }).click();
  await expect(page.getByText(/Oliver Berg moved from the waitlist/)).toBeVisible();
  await expect(detail(page).getByRole('heading', { name: 'Confirmed participants 1', exact: true })).toBeVisible();
  await expect(detail(page).getByRole('heading', { name: 'Waitlist 0', exact: true })).toBeVisible();
  await detail(page).getByRole('button', { name: "View Oliver Berg's schedule" }).click();
  await expect(page.getByRole('dialog').getByText(program.available.title, { exact: true })).toBeVisible();
  await expect(page.getByRole('dialog').getByRole('listitem').filter({ hasText: program.available.title }).getByText('Confirmed', { exact: true })).toBeVisible();
  await page.getByRole('dialog').press('Escape');
  await detail(page).getByRole('button', { name: 'Cancel registration for Oliver Berg', exact: true }).click();
  await expect(detail(page).getByText('1 available', { exact: true })).toBeVisible();
  await register(page, 1);
  await expect(page.getByText('Seat confirmed', { exact: true })).toBeVisible();
  await expect(detail(page).getByRole('heading', { name: 'Confirmed participants 1', exact: true })).toBeVisible();
});

test('conflicts, touching boundaries and skipped FIFO promotion are correct', async ({ page, request, program }) => {
  await apiRegister(request, program.available.id, 1);
  await apiRegister(request, program.overlap.id, 2);
  await apiRegister(request, program.overlap.id, 1);
  await apiRegister(request, program.overlap.id, 3);
  await page.goto('/');
  await choose(page, program.touching);
  await detail(page).getByLabel('Participant', { exact: true }).selectOption('2');
  await expect(detail(page).getByText(/Schedule conflict with/)).toBeVisible();
  await expect(detail(page).getByRole('button', { name: 'Register participant', exact: true })).toBeDisabled();
  await register(page, 1);
  await expect(page.getByText('Seat confirmed', { exact: true })).toBeVisible();
  await choose(page, program.overlap);
  await detail(page).getByRole('button', { name: 'Cancel registration for Oliver Berg' }).click();
  await expect(page.getByText(/Amara Okafor moved from the waitlist/)).toBeVisible();
  const confirmed = detail(page).getByRole('region', { name: 'Confirmed participants 1' });
  await expect(confirmed.getByRole('button', { name: "View Amara Okafor's schedule" })).toBeVisible();
  const waiting = detail(page).getByRole('region', { name: 'Waitlist 1' });
  await expect(waiting.getByRole('button', { name: "View Maya Chen's schedule" })).toBeVisible();
});

test('lost registration response retries with the same persisted key', async ({ page, request, program }) => {
  const keys: string[] = [];
  let loseResponse = true;
  await page.route(`**/api/workshops/${program.available.id}/registrations`, async route => {
    if (route.request().method() !== 'POST') return route.continue();
    keys.push(route.request().headers()['idempotency-key']);
    const response = await route.fetch();
    if (loseResponse) { loseResponse = false; await route.abort('failed'); }
    else await route.fulfill({ response });
  });
  await page.goto('/');
  await choose(page, program.available);
  await register(page, 1);
  await expect(page.getByText('Registration outcome is uncertain', { exact: true })).toBeVisible();
  await page.reload();
  await choose(page, program.available);
  await detail(page).getByLabel('Participant', { exact: true }).selectOption('1');
  await expect(detail(page).getByText(/previous response was lost/)).toBeVisible();
  await detail(page).getByRole('button', { name: 'Retry registration' }).click();
  await expect(page.getByText('Seat confirmed', { exact: true })).toBeVisible();
  expect(keys).toHaveLength(2);
  expect(keys[1]).toBe(keys[0]);
  const rows = await (await request.get(`/api/workshops/${program.available.id}/registrations`)).json() as Registration[];
  expect(rows.filter(item => item.status === 'Confirmed')).toHaveLength(1);
});

test('saved mutation with failed refresh is clearly distinguished and recoverable', async ({ page, program }) => {
  await page.goto('/');
  await choose(page, program.available);
  let failRefresh = true;
  await page.route('**/api/workshops', async route => {
    if (failRefresh) { failRefresh = false; await route.fulfill({ status: 503, json: { detail: 'Temporary refresh failure' } }); }
    else await route.continue();
  });
  await register(page, 1);
  await expect(page.getByText('Registration saved · refresh needed', { exact: true })).toBeVisible();
  await expect(page.getByText(/The change was saved/)).toBeVisible();
  await expect(detail(page).getByRole('button', { name: 'Register participant', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await expect(detail(page).getByText('1 of 1 seats filled', { exact: true })).toBeVisible();
  await expect(page.getByText('Registration saved · refresh needed', { exact: true })).not.toBeVisible();
});

test('slow refresh cannot change the newer workshop or participant selection', async ({ page, program }) => {
  await page.goto('/');
  await choose(page, program.available);
  await page.route('**/api/workshops', async route => {
    await new Promise(resolve => setTimeout(resolve, 300));
    await route.continue();
  });
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await choose(page, program.touching);
  await expect(page.getByRole('button', { name: 'Refresh', exact: true })).toBeEnabled();
  await expect(detail(page).getByRole('heading', { level: 2 })).toHaveText(program.touching.title);
  await detail(page).getByLabel('Participant', { exact: true }).selectOption('1');
  await detail(page).getByLabel('Participant', { exact: true }).selectOption('2');
  await register(page, 2);
  await expect(page.getByText(/Oliver Berg is registered/)).toBeVisible();
  await page.getByRole('button', { name: 'Participants', exact: true }).click();
  await page.getByRole('button', { name: /Oliver Berg oliver.berg/ }).click();
  await expect(page.getByRole('dialog').getByText(program.touching.title, { exact: true })).toBeVisible();
});

test('keyboard creation form validates and restores focus', async ({ page }) => {
  await page.goto('/');
  const newWorkshop = page.getByRole('button', { name: 'New workshop' });
  await expect(newWorkshop).toBeEnabled();
  await newWorkshop.focus();
  await page.keyboard.press('Enter');
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog.getByLabel('Workshop title')).toBeFocused();
  await dialog.getByLabel('Workshop title').fill('Keyboard-friendly collaboration');
  await dialog.getByLabel('Starts', { exact: true }).fill('2031-06-12T09:00');
  await dialog.getByLabel('Ends', { exact: true }).fill('2031-06-12T08:00');
  await dialog.getByLabel('Seat capacity').fill('10');
  await dialog.getByRole('button', { name: 'Create workshop' }).click();
  await expect(dialog.getByText(/after the start/)).toBeVisible();
  await dialog.getByLabel('Ends', { exact: true }).fill('2031-06-12T10:00');
  await dialog.getByRole('button', { name: 'Create workshop' }).click();
  await expect(page.getByText('Workshop created', { exact: true })).toBeVisible();
  await expect(newWorkshop).toBeFocused();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test('definitive rejection clears old feedback and starts a new registration attempt', async ({ page, program }) => {
  const keys: string[] = [];
  let rejectOnce = true;
  await page.route(`**/api/workshops/${program.available.id}/registrations`, async route => {
    if (route.request().method() !== 'POST') return route.continue();
    keys.push(route.request().headers()['idempotency-key']);
    if (rejectOnce) {
      rejectOnce = false;
      await route.fulfill({ status: 409, json: { detail: 'A conflicting registration was found.' } });
    } else await route.continue();
  });
  await page.goto('/');
  await choose(page, program.available);
  await register(page, 1);
  await expect(page.getByText('Registration could not be completed', { exact: true })).toBeVisible();
  await register(page, 2);
  await expect(page.getByText('Seat confirmed', { exact: true })).toBeVisible();
  await expect(page.getByText('A conflicting registration was found.', { exact: true })).not.toBeVisible();
  expect(keys).toHaveLength(2);
  expect(keys[1]).not.toBe(keys[0]);
});

test('primary registration, schedule and cancellation are keyboard usable', async ({ page, program }) => {
  await page.goto('/');
  await page.getByLabel('Search workshops').fill(program.available.title);
  const card = page.getByRole('button', { name: new RegExp(`^${program.available.title.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')},`) });
  await card.focus();
  await page.keyboard.press('Enter');
  await expect(detail(page).getByRole('heading', { level: 2 })).toHaveText(program.available.title);
  const participant = detail(page).getByLabel('Participant', { exact: true });
  await participant.focus();
  // Native select typeahead works across the macOS/headless picker behavior.
  await page.keyboard.press('a');
  await expect(participant).toHaveValue('3');
  await page.keyboard.press('Tab');
  await expect(detail(page).getByRole('button', { name: 'Register participant', exact: true })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.getByText('Seat confirmed', { exact: true })).toBeVisible();
  const schedule = detail(page).getByRole('button', { name: "View Amara Okafor's schedule", exact: true });
  await schedule.focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('dialog')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).not.toBeVisible();
  await expect(schedule).toBeFocused();
  const cancel = detail(page).getByRole('button', { name: 'Cancel registration for Amara Okafor', exact: true });
  await cancel.focus();
  await page.keyboard.press('Enter');
  await expect(page.getByText('Registration cancelled', { exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test('action feedback clears when another workshop is selected', async ({ page, program }) => {
  await page.goto('/');
  await choose(page, program.available);
  await register(page, 1);
  await register(page, 2);
  await expect(page.getByText('Added to the waitlist', { exact: true })).toBeVisible();
  await expect(page.getByText(`Oliver Berg joined the waitlist for ${program.available.title}.`, { exact: false })).toBeVisible();
  await choose(page, program.overlap);
  await expect(page.getByText('Added to the waitlist', { exact: true })).not.toBeVisible();
  await choose(page, program.available);
  await expect(page.getByText('Added to the waitlist', { exact: true })).not.toBeVisible();
});

test('delayed action feedback cannot appear in a different workshop', async ({ page, program }) => {
  let release!: () => void;
  const responseGate = new Promise<void>(resolve => { release = resolve; });
  await page.route(`**/api/workshops/${program.available.id}/registrations`, async route => {
    if (route.request().method() !== 'POST') return route.continue();
    const response = await route.fetch();
    await responseGate;
    await route.fulfill({ response });
  });
  await page.goto('/');
  await choose(page, program.available);
  await detail(page).getByLabel('Participant', { exact: true }).selectOption('1');
  await detail(page).getByRole('button', { name: 'Register participant', exact: true }).click();
  await choose(page, program.overlap);
  release();
  await expect(page.getByRole('button', { name: 'Refresh', exact: true })).toBeEnabled();
  await expect(detail(page).getByRole('heading', { level: 2 })).toHaveText(program.overlap.title);
  await expect(page.getByText('Seat confirmed', { exact: true })).not.toBeVisible();
});

test('lost workshop creation response survives reload and replays without duplicates', async ({ page, request }, info) => {
  const title = `Recovered workshop ${info.project.name}`;
  const keys: string[] = [];
  let loseResponse = true;
  await page.route('**/api/workshops', async route => {
    if (route.request().method() !== 'POST') return route.continue();
    keys.push(route.request().headers()['idempotency-key']);
    const response = await route.fetch();
    if (loseResponse) { loseResponse = false; await route.abort('failed'); }
    else await route.fulfill({ response });
  });
  await page.goto('/');
  await page.getByRole('button', { name: 'New workshop', exact: true }).click();
  let dialog = page.getByRole('dialog');
  await dialog.getByLabel('Workshop title').fill(title);
  await dialog.getByLabel('Starts', { exact: true }).fill('2032-06-12T23:00');
  await dialog.getByLabel('Ends', { exact: true }).fill('2032-06-13T01:00');
  await dialog.getByLabel('Seat capacity').fill('12');
  await dialog.getByRole('button', { name: 'Create workshop', exact: true }).click();
  await expect(dialog.getByText('Workshop creation outcome is uncertain', { exact: true })).toBeVisible();
  await expect(dialog.getByLabel('Workshop title')).toBeDisabled();
  await dialog.getByRole('button', { name: 'Close for now', exact: true }).click();
  await page.reload();
  await page.getByRole('button', { name: 'Resolve workshop', exact: true }).click();
  dialog = page.getByRole('dialog');
  await expect(dialog.getByLabel('Workshop title')).toHaveValue(title);
  await expect(dialog.getByLabel('Starts', { exact: true })).toHaveValue('2032-06-12T23:00');
  await expect(dialog.getByLabel('Ends', { exact: true })).toHaveValue('2032-06-13T01:00');
  await expect(dialog.getByRole('button', { name: 'Retry workshop creation', exact: true })).toBeFocused();
  await dialog.getByRole('button', { name: 'Retry workshop creation', exact: true }).click();
  await expect(page.getByText('Workshop created', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'New workshop', exact: true })).toBeEnabled();
  expect(keys).toHaveLength(2);
  expect(keys[1]).toBe(keys[0]);
  const workshops = await (await request.get('/api/workshops')).json() as Workshop[];
  const recovered = workshops.filter(workshop => workshop.title === title);
  expect(recovered).toHaveLength(1);
  await choose(page, recovered[0]);
  await expect(detail(page).getByText('12 Jun, 23:00 – 13 Jun, 01:00', { exact: true })).toBeVisible();
  await register(page, 6);
  await detail(page).getByRole('button', { name: "View Noah Williams's schedule", exact: true }).click();
  await expect(page.getByRole('dialog').getByText('12 Jun, 23:00 – 13 Jun, 01:00', { exact: true })).toBeVisible();
  await page.getByRole('dialog').press('Escape');
  await detail(page).getByRole('button', { name: 'Cancel registration for Noah Williams', exact: true }).click();
});

test('320-pixel reflow and enlarged text keep navigation, content and dialogs usable', async ({ page }, info) => {
  test.skip(info.project.name !== 'desktop', 'Reflow is independent of the starting viewport.');
  await page.setViewportSize({ width: 320, height: 900 });
  await page.goto('/');
  await expect(page.getByRole('button', { name: 'New workshop', exact: true })).toBeEnabled();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.getByRole('button', { name: 'Participants', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'People make the program', exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.getByRole('button', { name: 'Workshops', exact: true }).click();
  await page.getByRole('button', { name: 'New workshop', exact: true }).click();
  const dialog = page.getByRole('dialog');
  expect(await dialog.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
  await dialog.press('Escape');
  for (const width of [1280, 390, 320]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto('/');
    await expect(page.getByRole('button', { name: 'New workshop', exact: true })).toBeEnabled();
    // Double actual computed font sizes without changing the visual viewport.
    // This supplements320-CSS-pixel reflow; it is not a browser-zoom certification.
    await page.evaluate(() => {
      const sizes = [...document.querySelectorAll<HTMLElement>('body *')]
        .map(element => ({ element, size: parseFloat(getComputedStyle(element).fontSize) }));
      for (const { element, size } of sizes) element.style.fontSize = `${size * 2}px`;
    });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.getByRole('button', { name: 'Participants', exact: true }).click();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.getByRole('button', { name: 'Workshops', exact: true }).click();
    await page.getByRole('button', { name: 'New workshop', exact: true }).click();
    expect(await dialog.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
    await expect(dialog.getByLabel('Workshop title')).toBeVisible();
    await dialog.press('Escape');
    await expect(page.getByRole('button', { name: 'New workshop', exact: true })).toBeFocused();
  }
});
