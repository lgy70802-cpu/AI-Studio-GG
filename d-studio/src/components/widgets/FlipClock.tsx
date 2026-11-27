import React from 'react';
import './FlipClock.css';

export interface FlipClockProps {
  hours: string;
  minutes: string;
  seconds: string;
  showSeconds?: boolean;
  period?: string;
  portrait?: boolean;
  animate?: boolean;
}

// Time and pause state belong to the host so editor, thumbnails and USB output agree.
export function FlipClock({ hours, minutes, seconds, showSeconds = true, period, portrait = false, animate = true }: FlipClockProps) {
  const groups = showSeconds ? [hours, minutes, seconds] : [hours, minutes];
  return (
    <div className={`pixbuddy-flip-clock ${portrait ? 'pixbuddy-flip-clock-portrait' : ''}`}
      role="img" aria-label={`${groups.join(':')}${period ? ` ${period}` : ''}`}>
      {groups.map((value, groupIndex) => (
        <React.Fragment key={groupIndex}>
          {groupIndex > 0 && <span className="pixbuddy-flip-separator" aria-hidden="true">:</span>}
          <span className={`pixbuddy-flip-group ${groupIndex === 2 ? 'pixbuddy-flip-seconds' : ''}`} aria-hidden="true">
            {[...value.padStart(2, '0')].map((digit, index) => (
              <span className="pixbuddy-flip-card" key={index}>
                <span key={digit} className={animate ? 'pixbuddy-flip-digit' : ''}>{digit}</span>
              </span>
            ))}
          </span>
        </React.Fragment>
      ))}
      {period && <span className="pixbuddy-flip-period" aria-hidden="true">{period}</span>}
    </div>
  );
}
