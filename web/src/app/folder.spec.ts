import { buildFolderTree } from './folder';

describe('buildFolderTree', () => {
  it('nests children under their parent and returns name-sorted top-level nodes', () => {
    const tree = buildFolderTree([
      { id: 'b', parentId: null, name: 'Beta' },
      { id: 'a', parentId: null, name: 'Alpha' },
      { id: 'a1', parentId: 'a', name: 'Alpha-child' },
    ]);

    expect(tree.map((n) => n.name)).toEqual(['Alpha', 'Beta']);
    expect(tree[0].children.map((c) => c.name)).toEqual(['Alpha-child']);
    expect(tree[1].children).toEqual([]);
  });

  it('treats a node whose parent is missing as top-level (defensive)', () => {
    const tree = buildFolderTree([{ id: 'x', parentId: 'ghost', name: 'Orphan' }]);
    expect(tree.map((n) => n.id)).toEqual(['x']);
  });
});
