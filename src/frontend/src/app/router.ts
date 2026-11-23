import { createRouter, createWebHistory } from 'vue-router';
import ApplicationLayout from '../layout/application-layout/ApplicationLayout.vue';

export const router = createRouter({
  history: createWebHistory(),
  routes: [{ path: '/', component: ApplicationLayout, children: [
    { path: '', component: () => import('../features/explorer/mission-control/MissionControl.vue') },
    { path: 'repositories', component: () => import('../features/explorer/mission-control/MissionControl.vue'), props: { all: true } },
    { path: 'repositories/:repositoryId', component: () => import('../features/repositories/repository-details/RepositoryDetails.vue'), props: route => ({ workspaceId: 0, repositoryId: Number(route.params.repositoryId) }) },
    { path: 'settings', component: () => import('../features/settings/SettingsPage.vue') },
    { path: 'connection', component: () => import('../features/home/home-page/HomePage.vue') },
    { path: 'workspaces', component: () => import('../features/workspaces/workspaces-page/WorkspacesPage.vue') },
    { path: 'workspaces/:id', component: () => import('../features/workspaces/workspace-dashboard/WorkspaceDashboard.vue'), props: route => ({ workspaceId: Number(route.params.id) }) },
    { path: 'workspaces/:workspaceId/repositories/:repositoryId', component: () => import('../features/repositories/repository-details/RepositoryDetails.vue'), props: route => ({ workspaceId: Number(route.params.workspaceId), repositoryId: Number(route.params.repositoryId) }) },
  ] }],
});
