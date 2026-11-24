import type { GitFileChange } from '../../../core/repositories/models/repository-changes.model';

export interface FileTreeNode { name: string; path: string; children: FileTreeNode[]; file: GitFileChange | null; }
export function buildFileTree(files: readonly GitFileChange[]): FileTreeNode[] {
  const roots: FileTreeNode[] = [];
  for (const file of files) {
    const parts = file.path.split('/'); let children = roots; let path = '';
    parts.forEach((name, index) => {
      path += (path ? '/' : '') + name;
      const leaf = index === parts.length - 1;
      let node = children.find(item => item.path === path && Boolean(item.file) === leaf);
      if (!node) { node = { name, path, children: [], file: null }; children.push(node); }
      if (leaf) node.file = file;
      children = node.children;
    });
  }
  function sort(nodes: FileTreeNode[]) {
    nodes.sort((a, b) => Number(Boolean(a.file)) - Number(Boolean(b.file)) || a.name.localeCompare(b.name));
    nodes.forEach(node => sort(node.children));
  }
  sort(roots); return roots;
}
