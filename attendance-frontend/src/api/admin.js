import { api } from './client';

export const adminApi = {
  getEmployees: () => api.get('/admin/employees'),
  createEmployee: (data) => api.post('/admin/employees', data),
  updateEmployee: (id, data) => api.put(`/admin/employees/${id}`, data),
  deactivateEmployee: (id) => api.delete(`/admin/employees/${id}`),
  getReports: (month, year, includeInactive = false) => {
    const params = new URLSearchParams();
    if (month) params.set('month', month);
    if (year) params.set('year', year);
    if (includeInactive) params.set('includeInactive', 'true');
    const qs = params.toString();
    return api.get(`/admin/reports${qs ? '?' + qs : ''}`);
  },
  getAuditLogs: () => api.get('/admin/audit-logs'),
};
