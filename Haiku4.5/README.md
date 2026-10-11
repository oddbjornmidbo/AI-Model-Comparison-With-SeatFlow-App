# SeatFlow - Workshop Registration System

A full-stack workshop registration system built with ASP.NET Core (.NET 8), React + TypeScript, and SQLite.

## Architecture

### Backend (ASP.NET Core)
- **Framework**: ASP.NET Core 8.0 Web API
- **Database**: Entity Framework Core with SQLite
- **Key Components**:
  - Domain models: Workshop, Participant, Registration
  - RegistrationService: Core business logic with FIFO waitlist promotion
  - INotificationService: Abstraction for notifications (InMemoryNotificationService implementation provided)
  - REST API endpoints for workshops, registrations, and participant schedule

### Frontend (React + TypeScript + Vite)
- Single-page application with tab-based navigation
- Components:
  - WorkshopsList: View all available workshops
  - WorkshopDetail: View registrations, waitlist, and register participants
  - ParticipantSchedule: View confirmed workshop registrations
  - ParticipantForm: Add new participants
- HTTP client for communicating with backend

## Running the Application

### Backend

1. Navigate to the project directory:
```bash
cd /Users/oddbjornmidbo/Source/App-test/Haiku4.5
```

2. Run the backend:
```bash
dotnet run --project SeatFlow.Api
```

The API will start on `http://localhost:5000`

3. (Optional) Run tests:
```bash
dotnet test SeatFlow.Tests
```

### Frontend

1. Navigate to the frontend directory:
```bash
cd frontend
```

2. Install dependencies (if not already done):
```bash
npm install
```

3. Start the development server:
```bash
npm run dev
```

The UI will be available at `http://localhost:5173`

4. Build for production:
```bash
npm run build
```

## Business Logic Features

### Registration Management
- **Capacity Management**: Participants are automatically confirmed if capacity is available, waitlisted otherwise
- **Schedule Conflict Detection**: Prevents participants from registering for workshops with overlapping times
- **Duplicate Prevention**: Ensures each participant can only have one non-cancelled registration per workshop
- **Idempotency**: Registration requests with the same Idempotency-Key header are deduplicated

### Waitlist Promotion
- **FIFO Ordering**: Waitlisted registrations are promoted in creation order
- **Conflict Avoidance**: When promoting from waitlist, skips participants with schedule conflicts with other confirmed registrations
- **Automatic Promotion**: Triggered automatically when a confirmed registration is cancelled

### Concurrent Safety
- Database-level constraints ensure safe concurrent registration attempts
- Only one participant can take the final available seat

### Idempotent Cancellation
- Cancelling an already-cancelled registration is a no-op

## API Endpoints

### Workshops
- `GET /api/workshops` - List all workshops with capacity info
- `POST /api/workshops` - Create a new workshop

### Registrations
- `POST /api/workshops/{workshopId}/registrations` - Register a participant (supports Idempotency-Key header)
- `GET /api/workshops/{workshopId}/registrations` - View workshop registrations and waitlist
- `POST /api/registrations/{registrationId}/cancel` - Cancel a registration

### Participants
- `GET /api/participants` - List all participants
- `POST /api/participants` - Create a new participant
- `GET /api/participants/{participantId}/schedule` - View participant's confirmed registrations

## Database

The application uses SQLite with an in-memory seed of:
- 4 workshops with overlapping time slots
- 5 participants
- Automatic database initialization on startup

Database file: `seatflow.db` (created in the API working directory)

## Testing

Comprehensive xUnit tests cover:
- Basic registration (available capacity and full workshops)
- Waitlist promotion with FIFO ordering
- Schedule conflict detection and prevention
- Duplicate registration prevention
- Idempotency-key behavior
- Idempotent cancellation
- Concurrent registration safety
- Skipping ineligible waitlisted participants

Run tests:
```bash
dotnet test SeatFlow.Tests -c Release
```

## Key Assumptions

1. **No Authentication**: Workshop registration is open to any participant
2. **Simple Notifications**: Development implementation logs notifications in-memory
3. **Notification Failures Don't Rollback**: If notification service fails, the registration/cancellation still succeeds
4. **Time-based Conflicts**: Schedule conflicts are determined by time overlap (start < other.end AND other.start < end)
5. **No Capacity Modifications**: Workshop capacity is fixed after creation

## Known Limitations

1. **No Persistence of Idempotency Keys**: Idempotency keys are stored in-memory and will be lost on server restart
2. **Single-server Deployment**: Idempotency tracking is not distributed
3. **Simple Error Handling**: Validation errors return basic HTTP responses
4. **No Pagination**: Large datasets are not paginated in list endpoints
5. **Basic UI**: Frontend prioritizes functionality over aesthetics

## Frontend UI Features

- **Workshops Tab**: Browse all available workshops with capacity information
- **Workshop Details**: View confirmed registrations and waitlist, register new participants
- **Schedule Tab**: View confirmed registrations for a selected participant
- **Register Tab**: Add new participants to the system
- **Status Badges**: Clear visual distinction between Confirmed, Waitlisted, and Cancelled registrations
- **Error Handling**: User-friendly error messages and success notifications

## Build Status

- ✅ Backend: 0 errors, 0 warnings
- ✅ Backend Tests: 10/10 passing
- ✅ Frontend: Builds successfully
- ✅ API: Functional endpoints verified
