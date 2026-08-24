UPDATE AttendanceSessions
SET 
    StartTime = '08:00:00',
    EndTime = '09:30:00'
WHERE StartTime IS NULL;
