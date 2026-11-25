import type { DeviceInfo, ThemeItem } from '../data/themes';

export function matchesDeviceSize(theme: ThemeItem, device: DeviceInfo, mode: 'exact' | 'direction' = 'exact'): boolean {
  const portrait = device.orientation === 'portrait';
  const width = portrait ? device.nativeHeight : device.nativeWidth;
  const height = portrait ? device.nativeWidth : device.nativeHeight;
  const matches = (w: number, h: number) => mode === 'direction'
    ? w > 0 && h > 0 && (w >= h) === (width >= height)
    : w === width && h === height;
  if (theme.editorProject) {
    return theme.editorProject.layouts.some(layout => matches(layout.size[0], layout.size[1]));
  }
  const dimensions = theme.sourceWidth && theme.sourceHeight
    ? [theme.sourceWidth, theme.sourceHeight]
    : theme.resolution.match(/\d+/g)?.map(Number);
  return Boolean(dimensions && matches(dimensions[0], dimensions[1]));
}
