import { lazy, Suspense } from 'react';
import {createRoot} from 'react-dom/client';
import App from './App.tsx';
import './index.css';
import { DeviceRenderer } from './DeviceRenderer';

const output = new URLSearchParams(location.search).has('device-render');
if (output) {
  document.documentElement.style.cssText = 'margin:0;overflow:hidden;background:black';
  document.body.style.cssText = 'margin:0;overflow:hidden;background:black';
}
const root = createRoot(document.getElementById('root')!);
if (import.meta.env.DEV && new URLSearchParams(location.search).has('component-preview')) {
  const ComponentPreview = lazy(() => import('./dev/ComponentPreview'));
  root.render(<Suspense fallback={<p>正在加载组件预览…</p>}><ComponentPreview /></Suspense>);
} else {
  root.render(output ? <DeviceRenderer /> : <App />);
}
