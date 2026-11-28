import type { FileTreeNode } from './file-tree';
export interface VisibleChangeNode { node: FileTreeNode; depth: number; key: string; }
export function visibleChangeNodes(nodes: readonly FileTreeNode[], collapsed: readonly string[], depth = 0): VisibleChangeNode[] {
  return nodes.flatMap(node => [{ node, depth, key: (node.file ? 'f:' : 'd:') + node.path }, ...(!node.file && !collapsed.includes(node.path) ? visibleChangeNodes(node.children, collapsed, depth + 1) : [])]);
}
export function selectTreeItems(keys: readonly string[], previous: readonly string[], anchor: string | null, target: string, shift: boolean, control: boolean): string[] {
  if (shift && anchor !== null && keys.includes(anchor)) {
    const first = keys.indexOf(anchor); const last = keys.indexOf(target);
    const range = keys.slice(Math.min(first, last), Math.max(first, last) + 1);
    return control ? [...new Set([...previous, ...range])] : range;
  }
  if (control) return previous.includes(target) ? previous.filter(key => key !== target) : [...previous, target];
  return [target];
}
export function selectedFilePaths(keys: readonly string[], paths: readonly string[]) {
  return paths.filter(path => keys.some(key => key === 'f:' + path || key.startsWith('d:') && path.startsWith(key.slice(2) + '/')));
}
export function expandSelectedKeys(keys: readonly string[], nodes: readonly FileTreeNode[]) {
  const all = visibleChangeNodes(nodes, []);
  return [...new Set(keys.flatMap(key => key.startsWith('d:') ? all.filter(row => row.key === key || row.node.path.startsWith(key.slice(2) + '/')).map(row => row.key) : [key]))];
}
