import { defineConfig } from '@playwright/test';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const dotnet = (process.env.DOTNET_BIN || 'dotnet').replace(/'/g, "'\\''");

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  timeout: 30_000,
  use: { baseURL: 'http://127.0.0.1:5178', timezoneId: 'Europe/Oslo', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [
    { name: 'desktop', use: { viewport: { width: 1440, height: 900 }, colorScheme: 'light' } },
    { name: 'tablet', use: { viewport: { width: 768, height: 1024 }, colorScheme: 'light' } },
    { name: 'mobile', use: { viewport: { width: 390, height: 844 }, colorScheme: 'dark' } },
  ],
  webServer: [
    {
      command: `'${dotnet}' run --no-build --no-launch-profile --project ../backend/SeatFlow.Api`,
      url: 'http://127.0.0.1:5058/api/health',
      timeout: 60_000,
      reuseExistingServer: false,
      env: {
        ASPNETCORE_URLS: 'http://127.0.0.1:5058',
        ConnectionStrings__SeatFlow: `Data Source=${join(tmpdir(), `seatflow-browser-${process.pid}.sqlite`)};Foreign Keys=True;Default Timeout=30;Pooling=False`,
        Logging__LogLevel__Default: 'Warning',
      },
    },
    {
      command: 'npm run dev -- --port 5178',
      url: 'http://127.0.0.1:5178',
      timeout: 30_000,
      reuseExistingServer: false,
      env: { SEATFLOW_API_URL: 'http://127.0.0.1:5058' },
    },
  ],
});
