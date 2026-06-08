import { api } from './client';

export const clockApi = {
  clockIn: () => api.post('/clock/in'),
  clockOut: () => api.post('/clock/out'),
  status: () => api.get('/clock/status'),
  history: (month, year) => api.get(`/clock/history?month=${month}&year=${year}`),
};
