import { createRouter, createWebHistory } from 'vue-router';
import ApplicationLayout from '../layout/application-layout/ApplicationLayout.vue';

export const router = createRouter({
  history: createWebHistory(),
  routes: [{ path: '/', component: ApplicationLayout, children: [
    { path: '', component: () => import('../features/home/home-page/HomePage.vue') },
    { path: 'workspaces', component: () => import('../features/workspaces/workspaces-page/WorkspacesPage.vue') },
    { path: 'workspaces/:id', component: () => import('../features/workspaces/workspace-dashboard/WorkspaceDashboard.vue'), props: route => ({ workspaceId: Number(route.params.id) }) },
  ] }],
});
