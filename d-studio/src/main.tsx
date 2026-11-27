import {createRoot} from 'react-dom/client';
import App from './App.tsx';
import './index.css';
import { DeviceRenderer } from './DeviceRenderer';

const output = new URLSearchParams(location.search).has('device-render');
if (output) {
  document.documentElement.style.cssText = 'margin:0;overflow:hidden;background:black';
  document.body.style.cssText = 'margin:0;overflow:hidden;background:black';
}
createRoot(document.getElementById('root')!).render(output ? <DeviceRenderer /> : <App />);
