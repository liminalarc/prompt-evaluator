/** Mirrors the .NET FolderResponse DTO — one node of the folder tree. */
export interface Folder {
  id: string;
  parentId: string | null;
  name: string;
}

/**
 * A folder with its child folders resolved — the shape the sidebar renders. Built client-side
 * from the flat {@link Folder} list via `parentId`.
 */
export interface FolderNode extends Folder {
  children: FolderNode[];
}

/** Assembles the flat folder list into a tree of top-level {@link FolderNode}s (name-sorted). */
export function buildFolderTree(folders: Folder[]): FolderNode[] {
  const byId = new Map<string, FolderNode>();
  for (const f of folders) {
    byId.set(f.id, { ...f, children: [] });
  }
  const roots: FolderNode[] = [];
  for (const node of byId.values()) {
    if (node.parentId && byId.has(node.parentId)) {
      byId.get(node.parentId)!.children.push(node);
    } else {
      roots.push(node);
    }
  }
  const sort = (nodes: FolderNode[]) => {
    nodes.sort((a, b) => a.name.localeCompare(b.name));
    nodes.forEach((n) => sort(n.children));
  };
  sort(roots);
  return roots;
}
