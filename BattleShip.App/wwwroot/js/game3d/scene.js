import * as THREE from '../../lib/three.module.js';

const CELL = 1;

function makeBoard(gridSize, offsetX) {
    const group = new THREE.Group();
    const span = gridSize * CELL;

    const plate = new THREE.Mesh(
        new THREE.BoxGeometry(span, 0.3, span),
        new THREE.MeshStandardMaterial({ color: 0x0f2d3d, roughness: 0.8 }));
    plate.position.set(offsetX, 0.15, 0);
    group.add(plate);

    const lines = [];
    for (let i = 0; i <= gridSize; i++) {
        const p = i * CELL - span / 2;
        lines.push(offsetX + p, 0.31, -span / 2, offsetX + p, 0.31, span / 2);
        lines.push(offsetX - span / 2, 0.31, p, offsetX + span / 2, 0.31, p);
    }
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.Float32BufferAttribute(lines, 3));
    group.add(new THREE.LineSegments(
        geometry, new THREE.LineBasicMaterial({ color: 0x6f9db2, transparent: true, opacity: 0.55 })));

    group.userData.plate = plate;
    group.userData.originX = offsetX - span / 2;
    group.userData.originZ = -span / 2;
    group.userData.cellSize = CELL;
    group.userData.gridSize = gridSize;
    return group;
}

export function createScene(renderer) {
    const scene = new THREE.Scene();
    scene.add(new THREE.HemisphereLight(0x9fc6d8, 0x08161f, 1.2));
    const sun = new THREE.DirectionalLight(0xffffff, 0.9);
    sun.position.set(14, 30, 18);
    scene.add(sun);

    const seaGeometry = new THREE.PlaneGeometry(300, 300, 80, 80);
    seaGeometry.rotateX(-Math.PI / 2);
    const sea = new THREE.Mesh(
        seaGeometry,
        new THREE.MeshStandardMaterial({ color: 0x14536e, roughness: 0.5, metalness: 0.2 }));
    sea.position.y = -0.4;
    scene.add(sea);

    let boards = null;
    let marks = [];
    let ships = [];

    function clear(list) {
        for (const mesh of list) {
            scene.remove(mesh);
            mesh.geometry.dispose();
            mesh.material.dispose();
        }
        return [];
    }

    function cellCentre(board, x, y) {
        return {
            x: board.userData.originX + (x + 0.5) * board.userData.cellSize,
            z: board.userData.originZ + (y + 0.5) * board.userData.cellSize
        };
    }

    const MARK_COLOURS = { miss: 0x7f97a4, hit: 0xd08a3a, sunk: 0x8c3b3b };

    function setState(state) {
        if (!state) return;

        if (!boards) {
            const gap = state.gridSize * CELL * 0.9;
            boards = {
                own: makeBoard(state.gridSize, -gap),
                opponent: makeBoard(state.gridSize, gap)
            };
            scene.add(boards.own);
            scene.add(boards.opponent);
        }

        marks = clear(marks);
        ships = clear(ships);

        const addMark = (board, cell) => {
            const centre = cellCentre(board, cell.x, cell.y);
            const mesh = new THREE.Mesh(
                new THREE.CylinderGeometry(0.32, 0.32, 0.12, 16),
                new THREE.MeshStandardMaterial({ color: MARK_COLOURS[cell.state] || 0x7f97a4 }));
            mesh.position.set(centre.x, 0.38, centre.z);
            scene.add(mesh);
            marks.push(mesh);
        };

        for (const cell of state.opponent.cells) addMark(boards.opponent, cell);
        for (const cell of state.own.cells) addMark(boards.own, cell);

        const addShip = (board, s, colour, sunk) => {
            const long = s.size * CELL * 0.9;
            const mesh = new THREE.Mesh(
                new THREE.BoxGeometry(s.vertical ? 0.7 : long, 0.5, s.vertical ? long : 0.7),
                new THREE.MeshStandardMaterial({ color: colour, roughness: 0.45, metalness: 0.35 }));
            const centre = cellCentre(
                board,
                s.x + (s.vertical ? 0 : (s.size - 1) / 2),
                s.y + (s.vertical ? (s.size - 1) / 2 : 0));
            mesh.position.set(centre.x, sunk ? -0.1 : 0.55, centre.z);
            if (sunk) mesh.rotation.z = 0.3;
            scene.add(mesh);
            ships.push(mesh);
        };

        for (const s of state.own.ships) addShip(boards.own, s, 0x8fb8c9, s.sunk);
        for (const s of state.opponent.ships) addShip(boards.opponent, s, 0x6b3f3f, true);
    }

    function dispose() {
        marks = clear(marks);
        ships = clear(ships);

        if (boards) {
            for (const group of [boards.own, boards.opponent]) {
                scene.remove(group);
                group.traverse(o => {
                    if (o.geometry) o.geometry.dispose();
                    if (o.material) o.material.dispose();
                });
            }
            boards = null;
        }

        scene.remove(sea);
        seaGeometry.dispose();
        sea.material.dispose();
    }

    return { scene, get boards() { return boards; }, setState, dispose };
}
