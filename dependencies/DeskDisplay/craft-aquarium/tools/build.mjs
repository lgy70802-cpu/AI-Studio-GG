import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { aquariumConfig } from '../src/aquariumConfig.js';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const names = ['aquariumConfig','Fish','BubbleSystem','WaterLight','LightRay','FishSprites','assetLoader','Scene','Occlusion','Renderer','Aquarium','CraftClock','CraftTarget','main'];
const embedded = {};
for (const asset of [aquariumConfig.background, ...aquariumConfig.fishImages, aquariumConfig.causticsImage]) {
  const bytes = await fs.readFile(path.resolve(root, asset));
  const type = /\.jpe?g$/i.test(asset) ? 'image/jpeg' : 'image/png';
  embedded[asset] = `data:${type};base64,${bytes.toString('base64')}`;
}
let bundle = `window.aquariumEmbeddedAssets=${JSON.stringify(embedded)};\n`;
for (const name of names) {
  let code = await fs.readFile(path.join(root, 'src', name + '.js'), 'utf8');
  code = code.replace(/^import .*;\r?\n/gm, '').replace(/^export /gm, '');
  bundle += `\n// ${name}.js\n${code}\n`;
}
const template = await fs.readFile(path.join(root, 'index.html'), 'utf8');
const html = template.replace(/<!-- aquarium-entry -->[\s\S]*?<!-- \/aquarium-entry -->/, () => `<script>(()=>{\n${bundle}\n})();</script>`);
await fs.writeFile(path.join(root, 'preview.html'), html);
const launcher = file => '@echo off\r\nchcp 65001 >nul\r\nstart "" "%~dp0' + file + '"\r\n';
await fs.writeFile(path.join(root, '打开鱼缸.cmd'), launcher('preview.html'));
const out = path.join(root, 'release', 'Aquarium-V1.1', 'com.mirabox.streamdock.aquarium.mPanelPlugin');
await fs.mkdir(out, { recursive: true });
await fs.writeFile(path.join(root,'release','Aquarium-V1.1','浏览器预览.html'),html);
await fs.writeFile(path.join(root,'release','Aquarium-V1.1','打开鱼缸.cmd'), launcher('浏览器预览.html'));
await fs.writeFile(path.join(out,'index.html'), html.replace('data-target="preview"','data-target="craft"'));
await fs.cp(path.join(root,'assets'), path.join(out,'assets'), { recursive:true, filter: file => !file.endsWith('atlas.png') && !file.endsWith('PROMPTS.md') });
await fs.mkdir(path.join(out,'images'),{recursive:true});
await fs.writeFile(path.join(out,'images/icon.svg'), '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128"><rect width="128" height="128" rx="24" fill="#27141e"/><path d="M95 64Q68 29 35 53L17 40v48l18-13q33 24 60-11" fill="#eab3ac"/><circle cx="78" cy="59" r="4" fill="#27141e"/></svg>');
const uuid = 'com.mirabox.streamdock.aquarium.action1';
const manifest = {
  Name:'Aquarium',Description:'Quiet layered aquarium',Version:'1.1.0',APIVersion:'1.0',SDKVersion:1,Author:'DeskDisplay',
  Icon:'images/icon.svg',CategoryIcon:'images/icon.svg',Category:'Aquarium',CodePath:'index.html',
  OS:[{Platform:'windows',MinimumVersion:'10'}],
  Actions:[{UUID:uuid,Name:'Aquarium',Tooltip:'静水 · 动态鱼缸',Icon:'images/icon.svg',PropertyInspectorPath:'index.html',
    Settings:{ActionGeometry:{width:2557,height:615}},States:[{FontSize:'10',TitleAlignment:'bottom'}],
    UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']}]
};
await fs.writeFile(path.join(out,'manifest.json'),JSON.stringify(manifest,null,2));
await fs.writeFile(path.join(out,'zh_CN.json'),JSON.stringify({Name:'静水鱼缸',Category:'静水鱼缸',Description:'安静的超宽屏动态鱼缸',[uuid]:{Name:'动态鱼缸',Tooltip:'三层鱼群、柔和水光与气泡'},Localization:{}},null,2));
await fs.writeFile(path.join(out,'en.json'),JSON.stringify({Name:'Aquarium',Category:'Aquarium',Description:'Quiet layered aquarium',[uuid]:{Name:'Aquarium',Tooltip:'Quiet layered aquarium'},Localization:{}},null,2));
await fs.copyFile(path.join(root,'README.md'),path.join(root,'release','Aquarium-V1.1','README.md'));
console.log('Built preview.html and release/Aquarium-V1.1 (no installation performed).');
