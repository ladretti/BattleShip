import * as THREE from '../../lib/three.module.js';

export function createCamera(aspect) {
    const camera = new THREE.PerspectiveCamera(45, aspect, 0.1, 400);
    let target = new THREE.Vector3(0, 0, 0);

    function frame(width) {
        const wide = width >= 900;
        camera.position.set(0, wide ? 30 : 38, wide ? 34 : 44);
        camera.lookAt(target);
    }

    function lookAtCell(x, y, gridSize) {
        const half = gridSize / 2;
        target = new THREE.Vector3(x - half + 0.5, 0, y - half + 0.5);
        camera.lookAt(target);
    }

    frame(typeof window !== 'undefined' ? window.innerWidth : 1200);
    return { camera, frame, lookAtCell, dispose() { target = null; } };
}
