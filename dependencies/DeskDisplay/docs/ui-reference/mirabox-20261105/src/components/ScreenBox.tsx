import React from 'react';
import { Resource, Device } from '../types';
import { dims, fit } from '../data';
import { ArtDisplay } from './ArtDisplay';

interface ScreenBoxProps {
  resource?: Resource;
  device?: Device | { w: number; h: number; rot: number };
  mode?: 'contain' | 'cover';
  maxWidth?: number | string;
  maxHeight?: number | string;
  className?: string;
  layoutIndex?: number;
}

export const ScreenBox: React.FC<ScreenBoxProps> = ({
  resource,
  device,
  mode = 'contain',
  maxWidth,
  maxHeight,
  className = '',
  layoutIndex,
}) => {
  const targetDims: [number, number] = device
    ? dims(device)
    : resource?.sizes?.[0] || [1920, 462];

  const targetRatio = targetDims[0] / targetDims[1];

  let activeLayoutIndex = layoutIndex;
  if (activeLayoutIndex === undefined && resource) {
    if (device) {
      const f = fit(resource, device);
      activeLayoutIndex = f.index ?? 0;
    } else {
      activeLayoutIndex = 0;
    }
  }

  const contentSize: [number, number] =
    resource?.sizes?.[activeLayoutIndex ?? 0] || targetDims;
  const contentRatio = contentSize[0] / contentSize[1];

  // Calculate inner content width and height percentage inside container based on contain/cover
  let innerW = 100;
  let innerH = 100;

  const ratioDelta = Math.abs(
    1 - Math.min(targetRatio, contentRatio) / Math.max(targetRatio, contentRatio)
  );

  if (ratioDelta > 0.01) {
    if (mode === 'cover') {
      if (contentRatio > targetRatio) {
        innerW = (contentRatio / targetRatio) * 100;
      } else {
        innerH = (targetRatio / contentRatio) * 100;
      }
    } else {
      // contain
      if (contentRatio > targetRatio) {
        innerH = (targetRatio / contentRatio) * 100;
      } else {
        innerW = (contentRatio / targetRatio) * 100;
      }
    }
  }

  return (
    <div
      className={`relative overflow-hidden bg-[#131c2d] flex items-center justify-center shrink-0 rounded-[4px] shadow-sm select-none ${className}`}
      style={{
        aspectRatio: `${targetDims[0]} / ${targetDims[1]}`,
        maxWidth: maxWidth ?? '100%',
        maxHeight: maxHeight ?? '100%',
      }}
    >
      <div
        className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 overflow-hidden flex items-center justify-center"
        style={{
          width: `${innerW}%`,
          height: `${innerH}%`,
        }}
      >
        <ArtDisplay
          resource={resource}
          layoutIndex={activeLayoutIndex}
          vertical={contentSize[0] < contentSize[1]}
        />
      </div>
    </div>
  );
};
