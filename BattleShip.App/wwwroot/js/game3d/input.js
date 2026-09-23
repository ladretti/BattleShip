import * as THREE from '../../lib/three.module.js';

export function createInput(renderer, camera, getBoards, onPick) {
    const raycaster = new THREE.Raycaster();
    const pointer = new THREE.Vector2();
    let highlight = null;
    let scene = null;

    function cellUnder(event) {
        const boards = getBoards();
        if (!boards) return null;

        const rect = renderer.domElement.getBoundingClientRect();
        pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
        pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
        raycaster.setFromCamera(pointer, camera);

        const hits = raycaster.intersectObject(boards.opponent, true);
        if (hits.length === 0) return null;

        const board = boards.opponent;
        const point = hits[0].point;
        const x = Math.floor((point.x - board.userData.originX) / board.userData.cellSize);
        const y = Math.floor((point.z - board.userData.originZ) / board.userData.cellSize);
        const n = board.userData.gridSize;
        return (x >= 0 && x < n && y >= 0 && y < n) ? { x, y } : null;
    }

    function setHover(x, y) {
        const boards = getBoards();
        if (!boards) return;
        if (!scene) scene = boards.opponent.parent;

        if (highlight) { scene.remove(highlight); highlight.geometry.dispose(); highlight.material.dispose(); highlight = null; }
        if (x === null || x === undefined) return;

        const board = boards.opponent;
        const size = board.userData.cellSize;
        highlight = new THREE.Mesh(
            new THREE.BoxGeometry(size * 0.94, 0.06, size * 0.94),
            new THREE.MeshBasicMaterial({ color: 0xffd764, transparent: true, opacity: 0.45 }));
        highlight.position.set(
            board.userData.originX + (x + 0.5) * size, 0.34,
            board.userData.originZ + (y + 0.5) * size);
        scene.add(highlight);
    }

    function onMove(event) { const c = cellUnder(event); setHover(c ? c.x : null, c ? c.y : null); }
    function onClick(event) { const c = cellUnder(event); if (c) onPick(c.x, c.y); }
    function onLeave() { setHover(null); }

    renderer.domElement.addEventListener('pointermove', onMove);
    renderer.domElement.addEventListener('click', onClick);
    renderer.domElement.addEventListener('pointerleave', onLeave);

    return {
        setHover,
        dispose() {
            renderer.domElement.removeEventListener('pointermove', onMove);
            renderer.domElement.removeEventListener('click', onClick);
            renderer.domElement.removeEventListener('pointerleave', onLeave);
            setHover(null);
        }
    };
}
