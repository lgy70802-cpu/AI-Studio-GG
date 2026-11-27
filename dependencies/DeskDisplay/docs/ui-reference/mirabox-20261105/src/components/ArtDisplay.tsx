import React from 'react';
import { Resource } from '../types';

interface ArtDisplayProps {
  resource?: Resource;
  text?: string;
  color?: string;
  layoutIndex?: number;
  vertical?: boolean;
}

export const ArtDisplay: React.FC<ArtDisplayProps> = ({
  resource,
  text,
  color,
  layoutIndex = 0,
  vertical: forceVertical,
}) => {
  if (!resource || resource.id === 0) {
    return (
      <div className="art ink flex flex-col items-center justify-center p-2 text-slate-400">
        <span className="text-xs">未选择内容</span>
      </div>
    );
  }

  const data = resource.layoutData?.[layoutIndex] || {};
  const currentSize = resource.sizes[layoutIndex] || [1920, 462];
  const isVertical = forceVertical !== undefined ? forceVertical : currentSize[0] < currentSize[1];
  const styleClass = data.style || resource.style || 'ink';

  const label =
    text !== undefined
      ? text
      : data.text ||
        resource.text ||
        (resource.id === 2 ? 'CPU 24%' : '10:24');

  const textColor = color || data.color || resource.color;

  const bgStyle: React.CSSProperties = resource.url
    ? {
        backgroundImage: `url('${resource.url}')`,
        backgroundSize: 'cover',
        backgroundPosition: 'center',
      }
    : {};

  const isThemeOrClock = resource.type === '主题' || resource.type === '时钟';

  return (
    <div
      className={`art ${styleClass} ${isVertical ? 'vertical' : ''}`}
      style={bgStyle}
    >
      {isThemeOrClock && (
        <>
          <b
            style={textColor ? { color: textColor } : undefined}
            className="tracking-wider select-none"
          >
            {isVertical ? label.replace(':', '\n') : label}
          </b>
          <small className="opacity-80 font-mono tracking-widest uppercase select-none">
            {isVertical ? (
              <>
                FRI
                <br />
                10.30
              </>
            ) : resource.id === 2 ? (
              'GPU 32°　 RAM 48%'
            ) : (
              'FRIDAY · OCTOBER 30'
            )}
          </small>
        </>
      )}
    </div>
  );
};
