import * as THREE from '../lib/three.module.js';

const state = { renderer: null, scene: null, camera: null, sea: null, ships: [], raf: 0, frozen: false };

function disposeShips() {
    for (const mesh of state.ships) {
        state.scene.remove(mesh);
        mesh.geometry.dispose();
        mesh.material.dispose();
    }
    state.ships = [];
}

window.battleshipScene = {
    init(canvas) {
        try {
            state.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
        } catch (e) {
            return false;
        }
        if (!state.renderer) return false;

        state.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        state.scene = new THREE.Scene();
        state.camera = new THREE.PerspectiveCamera(45, 1, 0.1, 500);
        state.camera.position.set(0, 26, 34);
        state.camera.lookAt(0, 0, 0);

        state.scene.add(new THREE.HemisphereLight(0x9fc6d8, 0x0a1a24, 1.1));
        const sun = new THREE.DirectionalLight(0xffffff, 0.8);
        sun.position.set(12, 30, 18);
        state.scene.add(sun);

        const seaGeometry = new THREE.PlaneGeometry(200, 200, 96, 96);
        seaGeometry.rotateX(-Math.PI / 2);
        state.sea = new THREE.Mesh(
            seaGeometry,
            new THREE.MeshStandardMaterial({ color: 0x14384a, roughness: 0.75, metalness: 0.1 })
        );
        state.scene.add(state.sea);

        const resize = () => {
            const w = canvas.clientWidth || 1;
            const h = canvas.clientHeight || 1;
            state.renderer.setSize(w, h, false);
            state.camera.aspect = w / h;
            state.camera.updateProjectionMatrix();
        };
        resize();
        window.addEventListener('resize', resize);

        const base = seaGeometry.attributes.position.array.slice();
        const tick = (t) => {
            state.raf = requestAnimationFrame(tick);
            if (!state.frozen) {
                const p = seaGeometry.attributes.position;
                for (let i = 0; i < p.count; i++) {
                    const x = base[i * 3], z = base[i * 3 + 2];
                    p.array[i * 3 + 1] = Math.sin((x + t * 0.0012) * 0.18) * 0.5
                                       + Math.cos((z + t * 0.0009) * 0.22) * 0.4;
                }
                p.needsUpdate = true;
                seaGeometry.computeVertexNormals();
            }
            state.renderer.render(state.scene, state.camera);
        };
        state.raf = requestAnimationFrame(tick);
        return true;
    },

    update(scene) {
        if (!state.renderer) return;
        disposeShips();

        const half = scene.gridSize / 2;
        const place = (s, color, sunk) => {
            const long = s.size;
            const geometry = new THREE.BoxGeometry(
                s.vertical ? 0.7 : long * 0.9, 0.55, s.vertical ? long * 0.9 : 0.7);
            const mesh = new THREE.Mesh(
                geometry,
                new THREE.MeshStandardMaterial({ color, roughness: 0.5, metalness: 0.35 }));
            mesh.position.set(
                s.x - half + (s.vertical ? 0.5 : long / 2),
                sunk ? -0.9 : 0.35,
                s.y - half + (s.vertical ? long / 2 : 0.5));
            if (sunk) mesh.rotation.z = 0.35;
            state.scene.add(mesh);
            state.ships.push(mesh);
        };

        for (const s of scene.friendly) place(s, 0x8fb8c9, s.sunk);
        for (const s of scene.wrecks) place(s, 0x6b3f3f, true);
    },

    freeze(frozen) { state.frozen = !!frozen; },

    dispose() {
        cancelAnimationFrame(state.raf);
        if (state.scene) disposeShips();
        if (state.renderer) state.renderer.dispose();
        state.renderer = null;
        state.scene = null;
    }
};
