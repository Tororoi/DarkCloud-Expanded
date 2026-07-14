#!/usr/bin/env python3
r"""Dark Cloud .mds model surgery — extract a named node (and its subtree) into a standalone .mds.

Motivation: the fishing sign is a `kanban` frame buried inside Muska Lacka's oasis Georama part
(`gedit\e04\scene.scn`, model root `oasisu`). Nothing loads that model outside e04, and the pond /
oasis models exist ONLY inside per-town scene.scn archives — so the sign cannot be reached by any
runtime pointer trick. Carving it into its own small .mds makes it a loose file, which every Georama
town already loads three of (`gedit/e01/mds/e01a0[345]_0.mds`, hardcoded paths at 0x2029AFD0/AFF0/B010).

FORMAT (verified against LoadMDSFile__FPUi... @ 0x1262B0 and the 624-byte e01a03_0.mds):

  header (0x10):
    +0x00  char[4]  "MDS\0"
    +0x04  u32      version (1)
    +0x08  u32      nodeCount          <- the loader reads this at +8
    +0x0C  u32      nodeTableOffset    <- always 0x10

  node (0x70 each, table at nodeTableOffset):
    +0x00  u32      unknown (0)
    +0x04  u32      unknown (0x70 — equals the node stride)
    +0x08  char[32] name               <- loader strcpy's this to CFrame+0x118
    +0x28  u32      meshOffset         <- ABSOLUTE file offset of this node's "MDT" block; 0 = no mesh
    +0x2C  s32      parent             <- node index, -1 = root
    +0x30  float    matrix[4][4]       <- loader transposes it into the CFrame

  mesh ("MDT" block at meshOffset):
    +0x00  char[4]  "MDT\0"
    +0x04  u32      headerSize (0x40)
    +0x08  u32      totalSize          <- the whole MDT block, so it is self-delimiting
    ...    VU1 packet data, vertices, UVs, colours, and a trailing "HB<texture>" name

Because meshOffset is ABSOLUTE, every kept node's meshOffset must be rewritten when the blocks move.
That is the only fixup a rebuild needs — nothing inside an MDT block points outside itself.
"""
import struct, sys, argparse

NODE_SIZE = 0x70
HDR_SIZE = 0x10


class Node:
    __slots__ = ('idx', 'name', 'mesh_off', 'parent', 'mat', 'raw')

    def __init__(self, idx, raw):
        self.idx = idx
        self.raw = bytearray(raw)
        self.name = raw[0x08:0x28].split(b'\x00')[0].decode('latin1')
        self.mesh_off = struct.unpack_from('<I', raw, 0x28)[0]
        self.parent = struct.unpack_from('<i', raw, 0x2C)[0]
        self.mat = list(struct.unpack_from('<16f', raw, 0x30))


class Mds:
    def __init__(self, data):
        if data[:4] != b'MDS\x00':
            raise ValueError('not an MDS block')
        self.data = data
        self.version, self.count, self.tbl = struct.unpack_from('<3I', data, 4)
        self.nodes = [Node(i, data[self.tbl + i * NODE_SIZE: self.tbl + (i + 1) * NODE_SIZE])
                      for i in range(self.count)]

    def mesh_bytes(self, n):
        """The full MDT block for a node, or b'' if it has none. MDT is self-delimiting via +0x08."""
        if not n.mesh_off:
            return b''
        o = n.mesh_off
        if self.data[o:o + 4] != b'MDT\x00':
            raise ValueError(f'node {n.name}: meshOffset 0x{o:X} is not an MDT block')
        size = struct.unpack_from('<I', self.data, o + 8)[0]
        return self.data[o:o + size]

    def texture(self, n):
        m = self.mesh_bytes(n)
        i = m.find(b'HB')
        return m[i + 2:i + 18].split(b'\x00')[0].decode('latin1', 'replace') if i >= 0 else ''

    def children(self, i):
        return [n for n in self.nodes if n.parent == i]

    def subtree(self, root_idx):
        """root_idx plus every descendant, parents-before-children."""
        out, stack = [], [root_idx]
        while stack:
            i = stack.pop(0)
            out.append(i)
            stack += [c.idx for c in self.children(i)]
        return out

    def find(self, name):
        for n in self.nodes:
            if n.name == name:
                return n
        return None


def build(mds, keep_idx, detach_root=True, recenter=False, unrotate=False):
    """Emit a standalone .mds containing only `keep_idx` (a parents-first index list).

    detach_root re-roots the first kept node (parent = -1) so the model stands alone at the origin
    rather than inheriting a transform from ancestors we are dropping.

    recenter zeroes the root's translation row, so the model's origin sits ON the object instead of
    wherever it happened to stand inside the donor model. Without this, the extracted sign would
    appear 90/2/64 units away from wherever you place it.

    unrotate resets the root's 3x3 to identity, dropping the donor's incidental yaw so the sign faces
    a predictable direction and can be aimed at runtime with CMapParts::SetRotY.
    """
    remap = {old: new for new, old in enumerate(keep_idx)}
    nodes_blob = bytearray()
    meshes_blob = bytearray()
    mesh_base = HDR_SIZE + len(keep_idx) * NODE_SIZE

    for new, old in enumerate(keep_idx):
        n = mds.nodes[old]
        raw = bytearray(n.raw)

        mesh = mds.mesh_bytes(n)
        if mesh:
            struct.pack_into('<I', raw, 0x28, mesh_base + len(meshes_blob))
            meshes_blob += mesh
        else:
            struct.pack_into('<I', raw, 0x28, 0)

        if new == 0 and detach_root:
            struct.pack_into('<i', raw, 0x2C, -1)
        else:
            struct.pack_into('<i', raw, 0x2C, remap.get(n.parent, -1))

        if new == 0:
            if recenter:                       # translation row (matrix[3][0..2]) -> 0
                struct.pack_into('<3f', raw, 0x30 + 12 * 4, 0.0, 0.0, 0.0)
            if unrotate:                       # upper 3x3 -> identity, translation row untouched
                for r in range(3):
                    for c in range(3):
                        struct.pack_into('<f', raw, 0x30 + (r * 4 + c) * 4,
                                         1.0 if r == c else 0.0)

        nodes_blob += raw

    hdr = struct.pack('<4s3I', b'MDS\x00', mds.version, len(keep_idx), HDR_SIZE)
    return bytes(hdr + nodes_blob + meshes_blob)


def cmd_list(mds, args):
    print(f"MDS v{mds.version}  {mds.count} nodes")
    print(f"{'idx':>4} {'parent':>6} {'mesh':>9} {'tex':<10} name")
    for n in mds.nodes:
        depth = 0
        p = n.parent
        seen = 0
        while p >= 0 and seen < 32:
            depth += 1
            p = mds.nodes[p].parent
            seen += 1
        msz = len(mds.mesh_bytes(n))
        print(f"{n.idx:>4} {n.parent:>6} {msz:>9} {mds.texture(n):<10} {'  ' * depth}{n.name}")


def cmd_extract(mds, args):
    root = mds.find(args.node)
    if root is None:
        sys.exit(f"node '{args.node}' not found")
    keep = mds.subtree(root.idx)
    print(f"extracting '{args.node}' (node {root.idx}) + {len(keep) - 1} descendant(s):")
    for i in keep:
        n = mds.nodes[i]
        print(f"   {n.name:<24} mesh={len(mds.mesh_bytes(n)):>7} B  tex={mds.texture(n)}")
    out = build(mds, keep, detach_root=not args.keep_transform,
                recenter=args.recenter, unrotate=args.unrotate)
    open(args.out, 'wb').write(out)
    print(f"\nwrote {args.out}  ({len(out)} bytes, {len(keep)} nodes)")
    # re-parse as a sanity check
    chk = Mds(out)
    for n in chk.nodes:
        chk.mesh_bytes(n)
    print(f"verified: re-parses cleanly, {chk.count} nodes, meshes resolve")


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('file', help='.mds file (use mds_extract_scene.py to pull one out of a scene.scn)')
    sub = ap.add_subparsers(dest='cmd', required=True)
    p = sub.add_parser('list', help='dump the node tree')
    p.set_defaults(fn=cmd_list)
    p = sub.add_parser('extract', help='carve a node + its subtree into a standalone .mds')
    p.add_argument('node')
    p.add_argument('-o', '--out', required=True)
    p.add_argument('--keep-transform', action='store_true',
                   help='keep the original parent link instead of re-rooting at the origin')
    p.add_argument('--recenter', action='store_true',
                   help="zero the root's translation so the model's origin sits on the object")
    p.add_argument('--unrotate', action='store_true',
                   help="reset the root's 3x3 to identity, dropping the donor's incidental yaw")
    p.set_defaults(fn=cmd_extract)
    args = ap.parse_args()
    mds = Mds(open(args.file, 'rb').read())
    args.fn(mds, args)


if __name__ == '__main__':
    main()
