import React, { useState } from 'react';
import { X, Search } from 'lucide-react';
import { ContentCategory, DeviceInfo, ThemeItem } from '../data/themes';
import { PortraitPosterCard } from './PortraitPosterCard';
import { LandscapePosterCard } from './LandscapePosterCard';

interface ContentPickerModalProps {
  open: boolean;
  onClose: () => void;
  themes: ThemeItem[];
  activeThemeId: string;
  activeDevice: DeviceInfo;
  onSelectTheme: (theme: ThemeItem) => void;
}

export const ContentPickerModal: React.FC<ContentPickerModalProps> = ({
  open,
  onClose,
  themes,
  activeThemeId,
  activeDevice,
  onSelectTheme,
}) => {
  const [query, setQuery] = useState('');
  const [tab, setTab] = useState<ContentCategory | '全部'>('全部');

  if (!open) return null;

  const isPortrait = activeDevice.orientation === 'portrait';

  const list = themes.filter((t) => {
    if (tab !== '全部' && t.category !== tab) return false;
    if (
      query.trim() !== '' &&
      !t.displayName.toLowerCase().includes(query.toLowerCase()) &&
      !t.name.toLowerCase().includes(query.toLowerCase())
    ) {
      return false;
    }
    return true;
  });

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-[#140B36]/45 p-4 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-label="选择并更换副屏内容"
    >
      <div
        className={`w-full overflow-hidden rounded-[26px] border border-white/60 bg-[#F7F7FC] shadow-[0_30px_80px_rgba(20,10,60,0.3)] ${
          isPortrait ? 'max-w-4xl' : 'max-w-3xl'
        }`}
      >
        <div className="flex items-center justify-between border-b border-[#ECEAF6] bg-white px-6 py-4">
          <div>
            <h3 className="text-[17px] font-bold text-[#1A172B]">更换副屏内容</h3>
            <p className="text-[12px] text-[#7A7690]">
              选择壁纸或动态时钟立即输出至 {activeDevice.name}（{activeDevice.resolution}）
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="关闭窗口"
            className="flex h-8 w-8 items-center justify-center rounded-full bg-[#F4F3FA] text-[#56526B] hover:bg-[#E9E6F7]"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3 px-6 pt-4">
          <div className="flex items-center rounded-xl bg-white p-1 shadow-sm">
            {(['全部', '动态时钟', '壁纸', '主题', '视频'] as const).map((cat) => (
              <button
                key={cat}
                type="button"
                onClick={() => setTab(cat)}
                className={`rounded-lg px-3 py-1.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                  tab === cat ? 'bg-[#5E45F8] text-white' : 'text-[#6B6880] hover:text-[#1A172B]'
                }`}
              >
                {cat}
              </button>
            ))}
          </div>

          <div className="flex items-center rounded-xl border border-[#ECEAF6] bg-white px-3 py-1.5">
            <Search className="h-3.5 w-3.5 text-[#9A96AE]" />
            <input
              type="text"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="快速搜索名称..."
              className="w-44 bg-transparent pl-2 text-[12.5px] text-[#1A172B] focus:outline-none"
            />
          </div>
        </div>

        <div
          className={`grid max-h-[520px] gap-4 overflow-y-auto p-6 studio-scroll ${
            isPortrait
              ? 'grid-cols-2 sm:grid-cols-3 md:grid-cols-4'
              : 'grid-cols-1 sm:grid-cols-2'
          }`}
        >
          {list.map((item) => {
            const isSelected = item.id === activeThemeId;
            const handleSelect = (selected: ThemeItem) => {
              onSelectTheme(selected);
              onClose();
            };

            if (isPortrait) {
              return (
                <PortraitPosterCard
                  key={item.id}
                  item={item}
                  activeDevice={activeDevice}
                  isCurrent={isSelected}
                  onApply={handleSelect}
                  subtitleExtra={activeDevice.resolution}
                />
              );
            }

            return (
              <LandscapePosterCard
                key={item.id}
                item={item}
                activeDevice={activeDevice}
                isCurrent={isSelected}
                onApply={handleSelect}
              />
            );
          })}
        </div>
      </div>
    </div>
  );
};

