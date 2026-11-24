export interface CommitReference { label: string; fullName: string; kind: 'branch' | 'remote' | 'tag' | 'head'; current: boolean; }

export function parseCommitReferences(value: string): CommitReference[] {
  if (!value.trim()) return [];
  return value.split(', ').map(fullName => {
    const current = fullName.startsWith('HEAD -> ') || fullName === 'HEAD';
    const name = fullName.replace(/^HEAD -> /u, '').replace(/^tag: /u, '');
    const kind = name.startsWith('refs/heads/') ? 'branch' : name.startsWith('refs/remotes/') ? 'remote' : name.startsWith('refs/tags/') ? 'tag' : 'head';
    return { fullName, current, kind, label: name.replace(/^refs\/(heads|remotes|tags)\//u, '') };
  });
}

export function formatCommitDate(value: string, language: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(language, { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }).format(date);
}
