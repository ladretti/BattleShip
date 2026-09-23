import * as THREE from '../../lib/three.module.js';
import { createScene } from './scene.js';
import { createCamera } from './camera.js';
import { createInput } from './input.js';

const state = { renderer: null, world: null, cam: null, input: null, raf: 0, frozen: false, resize: null, ref: null };

window.battleshipGame3d = {
    init(canvas, dotnetRef) {
        try {
            state.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
        } catch (e) {
            return false;
        }

        state.ref = dotnetRef;
        state.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        state.world = createScene(state.renderer);
        state.cam = createCamera(1);
        state.input = createInput(
            state.renderer, state.cam.camera, () => state.world.boards,
            (x, y) => { if (state.ref) state.ref.invokeMethodAsync('PickAsync', x, y); });

        const resize = () => {
            const w = canvas.clientWidth || 1, h = canvas.clientHeight || 1;
            state.renderer.setSize(w, h, false);
            state.cam.camera.aspect = w / h;
            state.cam.camera.updateProjectionMatrix();
            state.cam.frame(w);
        };
        state.resize = resize;
        resize();
        window.addEventListener('resize', resize);

        const tick = () => {
            state.raf = requestAnimationFrame(tick);
            state.renderer.render(state.world.scene, state.cam.camera);
        };
        state.raf = requestAnimationFrame(tick);
        return true;
    },

    update(scene) { if (state.world) state.world.setState(scene); },

    hover(x, y) { if (state.input) state.input.setHover(x, y); },

    freeze(frozen) { state.frozen = !!frozen; },

    dispose() {
        cancelAnimationFrame(state.raf);
        if (state.resize) { window.removeEventListener('resize', state.resize); state.resize = null; }
        if (state.input) { state.input.dispose(); state.input = null; }
        if (state.world) { state.world.dispose(); state.world = null; }
        if (state.cam) { state.cam.dispose(); state.cam = null; }
        if (state.renderer) { state.renderer.dispose(); state.renderer = null; }
        state.ref = null;
        state.frozen = false;
    }
};
