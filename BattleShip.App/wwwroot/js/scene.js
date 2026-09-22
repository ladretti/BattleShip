import * as THREE from '../lib/three.module.js';

const state = {
    renderer: null, scene: null, camera: null, sea: null, ships: [], raf: 0, frozen: false, resize: null,
    impactMesh: null, impactKey: null, impactStart: 0, revealLight: null
};

const IMPACT_PULSE_MS = 700;

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

        state.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        state.scene = new THREE.Scene();
        state.camera = new THREE.PerspectiveCamera(45, 1, 0.1, 500);
        state.camera.position.set(0, 26, 34);
        state.camera.lookAt(0, 0, 0);

        state.scene.add(new THREE.HemisphereLight(0x9fc6d8, 0x0a1a24, 1.1));
        const sun = new THREE.DirectionalLight(0xffffff, 0.8);
        sun.position.set(12, 30, 18);
        state.scene.add(sun);

        state.revealLight = new THREE.PointLight(0xffd27a, 0, 70, 0);
        state.revealLight.position.set(0, 6, 0);
        state.scene.add(state.revealLight);

        state.impactMesh = new THREE.Mesh(
            new THREE.RingGeometry(0.55, 0.85, 24),
            new THREE.MeshBasicMaterial({ color: 0xffd764, transparent: true, opacity: 0, side: THREE.DoubleSide }));
        state.impactMesh.rotation.x = -Math.PI / 2;
        state.impactMesh.visible = false;
        state.scene.add(state.impactMesh);

        const seaGeometry = new THREE.PlaneGeometry(200, 200, 96, 96);
        seaGeometry.rotateX(-Math.PI / 2);
        state.sea = new THREE.Mesh(
            seaGeometry,
            new THREE.MeshStandardMaterial({ color: 0x1d5f7e, roughness: 0.55, metalness: 0.15 })
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
        state.resize = resize;
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

                if (state.impactMesh.visible) {
                    const progress = Math.min((t - state.impactStart) / IMPACT_PULSE_MS, 1);
                    state.impactMesh.scale.setScalar(1 + progress * 0.8);
                    state.impactMesh.material.opacity = 0.55 * (1 - progress) + 0.12;
                }
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

        if (scene.lastImpact) {
            const key = `${scene.lastImpact.x},${scene.lastImpact.y}`;
            state.impactMesh.position.set(scene.lastImpact.x - half + 0.5, 0.4, scene.lastImpact.y - half + 0.5);
            state.impactMesh.visible = true;
            if (key !== state.impactKey) {
                state.impactKey = key;
                state.impactStart = performance.now();
                state.impactMesh.scale.setScalar(1);
                state.impactMesh.material.opacity = 0.55;
            }
        } else {
            state.impactMesh.visible = false;
            state.impactMesh.material.opacity = 0;
            state.impactKey = null;
        }

        state.revealLight.intensity = scene.revealed ? 6 : 0;
    },

    freeze(frozen) { state.frozen = !!frozen; },

    dispose() {
        cancelAnimationFrame(state.raf);
        if (state.resize) {
            window.removeEventListener('resize', state.resize);
            state.resize = null;
        }
        if (state.scene) disposeShips();
        if (state.sea) {
            state.sea.geometry.dispose();
            state.sea.material.dispose();
            state.sea = null;
        }
        if (state.impactMesh) {
            if (state.scene) state.scene.remove(state.impactMesh);
            state.impactMesh.geometry.dispose();
            state.impactMesh.material.dispose();
            state.impactMesh = null;
        }
        if (state.revealLight) {
            if (state.scene) state.scene.remove(state.revealLight);
            state.revealLight = null;
        }
        state.impactKey = null;
        state.impactStart = 0;
        if (state.renderer) state.renderer.dispose();
        state.renderer = null;
        state.scene = null;
        state.frozen = false;
    }
};
