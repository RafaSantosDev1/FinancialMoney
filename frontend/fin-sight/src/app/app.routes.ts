import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./layout/shell/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard').then((m) => m.Dashboard),
        title: 'Dashboard · FinSight',
      },
      {
        path: 'transactions',
        loadComponent: () =>
          import('./features/transactions/transactions').then((m) => m.Transactions),
        title: 'Transactions · FinSight',
      },
      {
        path: 'import',
        loadComponent: () => import('./features/import/import').then((m) => m.ImportPage),
        title: 'Import CSV · FinSight',
      },
      {
        path: 'categories',
        loadComponent: () =>
          import('./features/categories/categories').then((m) => m.Categories),
        title: 'Categories · FinSight',
      },
      {
        path: 'budgets',
        loadComponent: () => import('./features/budgets/budgets').then((m) => m.Budgets),
        title: 'Budgets · FinSight',
      },
      {
        path: 'reports',
        loadComponent: () => import('./features/reports/reports').then((m) => m.Reports),
        title: 'Reports · FinSight',
      },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
