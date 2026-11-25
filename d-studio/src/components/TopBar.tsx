import { desktopMode } from '../services/desktopBridge';
import React, { useState, useRef, useEffect } from 'react';
import {
  Search,
  SlidersHorizontal,
  HelpCircle,
  Bell,
  ChevronDown,
  Monitor,
  Check,
  User,
  Menu,
} from 'lucide-react';
import { ContentCategory, DeviceInfo, NavSection } from '../data/themes';

interface TopBarProps {
  devices: DeviceInfo[];
  activeDevice: DeviceInfo;
  onSelectDevice: (deviceId: string) => void;
  searchQuery: string;
  onSearchChange: (q: string) => void;
  categoryFilter: ContentCategory | '全部';
  onCategoryFilterChange: (cat: ContentCategory | '全部') => void;
  onSelectNav: (nav: NavSection) => void;
  onOpenMobileMenu: () => void;
  recentLogs: string[];
}

export const TopBar: React.FC<TopBarProps> = ({
  devices,
  activeDevice,
  onSelectDevice,
  searchQuery,
  onSearchChange,
  categoryFilter,
  onCategoryFilterChange,
  onSelectNav,
  onOpenMobileMenu,
  recentLogs,
}) => {
  const [deviceOpen, setDeviceOpen] = useState(false);
  const [filterOpen, setFilterOpen] = useState(false);
  const [notifOpen, setNotifOpen] = useState(false);
  const [helpOpen, setHelpOpen] = useState(false);
  const [profileOpen, setProfileOpen] = useState(false);

  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setDeviceOpen(false);
        setFilterOpen(false);
        setNotifOpen(false);
        setHelpOpen(false);
        setProfileOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const closeAll = () => {
    setDeviceOpen(false);
    setFilterOpen(false);
    setNotifOpen(false);
    setHelpOpen(false);
    setProfileOpen(false);
  };

  return (
    <header
      ref={containerRef}
      className="relative z-30 flex flex-wrap md:flex-nowrap items-center justify-between gap-4 border-b border-[#ECEAF6] bg-white/60 px-6 py-4 backdrop-blur-md"
    >
      {/* Left Zone: Mobile menu trigger + Device Dropdown Selector */}
      <div className="flex items-center gap-2.5 shrink-0">
        <button
          type="button"
          onClick={onOpenMobileMenu}
          className="flex h-9 w-9 items-center justify-center rounded-xl border border-[#E6E4F2] bg-white text-[#4A4660] lg:hidden"
          aria-label="打开菜单"
        >
          <Menu className="h-4 w-4" />
        </button>

        {/* Single Device Selector Dropdown */}
        <div className="relative">
          <button
            type="button"
            onClick={() => {
              const next = !deviceOpen;
              closeAll();
              setDeviceOpen(next);
            }}
            aria-label="选择设备"
            className="flex h-9 items-center gap-2 rounded-full border border-[#E6E4F2] bg-white px-3.5 text-[12.5px] font-semibold text-[#1A172B] shadow-[0_2px_10px_rgba(30,20,80,0.03)] transition-colors hover:border-[#C8BDFC] hover:text-[#5E45F8] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8] whitespace-nowrap"
          >
            <Monitor className="h-3.5 w-3.5 text-[#5E45F8]" />
            <span>
              {activeDevice.name.split('·')[0].trim()} ·{' '}
              {activeDevice.orientation === 'landscape' ? '横屏' : '竖屏'}
            </span>
            <ChevronDown className="h-3.5 w-3.5 text-[#8A86A0]" />
          </button>

          {deviceOpen && (
            <div className="absolute left-0 top-full mt-2 w-76 rounded-2xl border border-[#ECEAF6] bg-white p-2 shadow-[0_18px_40px_rgba(25,15,75,0.12)]">
              <div className="px-3 py-1.5 text-[11px] font-semibold text-[#9A96AE]">
                已接入硬件副屏（{devices.length} 台）
              </div>
              {devices.map((dev) => {
                const selected = dev.id === activeDevice.id;
                return (
                  <button
                    key={dev.id}
                    type="button"
                    onClick={() => {
                      onSelectDevice(dev.id);
                      setDeviceOpen(false);
                    }}
                    className={`flex w-full items-center justify-between rounded-xl px-3 py-2.5 text-left transition-colors ${
                      selected ? 'bg-[#F3F0FF] text-[#5E45F8]' : 'hover:bg-[#F7F6FC] text-[#1A172B]'
                    }`}
                  >
                    <div>
                      <div className="text-[13px] font-semibold">{dev.name}</div>
                      <div className="font-mono-tabular text-[11px] text-[#7A7690]">
                        {dev.resolution} · {dev.orientation === 'landscape' ? '横屏' : '竖屏'}
                        {dev.flipped ? ' · 左右翻转' : ''}
                      </div>
                    </div>
                    {selected && <Check className="h-4 w-4 text-[#5E45F8]" />}
                  </button>
                );
              })}
              <div className="mt-1 border-t border-[#F1EFF8] pt-1">
                <button
                  type="button"
                  onClick={() => {
                    setDeviceOpen(false);
                    onSelectNav('devices');
                  }}
                  className="flex w-full items-center justify-center rounded-xl py-2 text-[12px] font-semibold text-[#5E45F8] hover:bg-[#F5F2FF]"
                >
                  打开设备管理中心
                </button>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Center Zone: Figure 2 Pill Search Bar with Purple Filter Button */}
      <div className="relative order-last w-full basis-full md:order-none md:basis-auto md:max-w-[440px] md:flex-1">
        <div className="flex items-center rounded-full border border-[#E9E7F4] bg-[#EFEFF7]/90 pl-4 pr-1.5 py-1 transition-all focus-within:border-[#C4B7FF] focus-within:bg-white focus-within:shadow-[0_6px_20px_rgba(94,69,248,0.1)]">
          <Search className="h-4 w-4 shrink-0 text-[#8B87A1]" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder="搜索壁纸、动态时钟、主题名称..."
            aria-label="搜索主题与壁纸"
            className="w-full bg-transparent px-3 py-1.5 text-[13px] text-[#1A172B] placeholder:text-[#9A96AE] focus:outline-none"
          />
          <button
            type="button"
            onClick={() => {
              const next = !filterOpen;
              closeAll();
              setFilterOpen(next);
            }}
            aria-label="筛选内容分类"
            className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-[#5E45F8] text-white shadow-[0_4px_12px_rgba(94,69,248,0.35)] transition-transform duration-150 hover:scale-105 hover:bg-[#4D34E6] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8]"
          >
            <SlidersHorizontal className="h-3.5 w-3.5" />
          </button>
        </div>

        {filterOpen && (
          <div className="absolute right-0 top-full mt-2 w-60 rounded-2xl border border-[#ECEAF6] bg-white p-3 shadow-[0_18px_40px_rgba(25,15,75,0.12)]">
            <div className="mb-2 text-[11px] font-semibold text-[#9A96AE]">内容类型筛选</div>
            <div className="grid grid-cols-2 gap-1.5">
              {(['全部', '动态时钟', '壁纸', '主题', '视频'] as const).map((cat) => (
                <button
                  key={cat}
                  type="button"
                  onClick={() => {
                    onCategoryFilterChange(cat);
                    setFilterOpen(false);
                  }}
                  className={`rounded-xl px-3 py-2 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                    categoryFilter === cat
                      ? 'bg-[#5E45F8] text-white'
                      : 'bg-[#F5F4FA] text-[#56526B] hover:bg-[#EBE8FA]'
                  }`}
                >
                  {cat}
                </button>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Right Zone: Help, Notifications, Profile Avatar */}
      <div className="flex items-center gap-2.5 shrink-0">
        {/* Help Button */}
        <div className="relative">
          <button
            type="button"
            onClick={() => {
              const next = !helpOpen;
              closeAll();
              setHelpOpen(next);
            }}
            aria-label="使用帮助"
            className="flex h-9 w-9 items-center justify-center rounded-full border border-[#ECEAF6] bg-white text-[#56526B] transition-colors hover:border-[#C8BDFC] hover:text-[#5E45F8] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8]"
          >
            <HelpCircle className="h-4 w-4" />
          </button>
          {helpOpen && (
            <div className="absolute right-0 top-full mt-2 w-72 rounded-2xl border border-[#ECEAF6] bg-white p-4 shadow-[0_18px_40px_rgba(25,15,75,0.12)]">
              <div className="text-[13px] font-bold text-[#1A172B]">快捷操作提示</div>
              <ul className="mt-2.5 space-y-2 text-[12px] text-[#6B6880]">
                <li>· 单击任意壁纸或主题卡片即可实时推送到 {activeDevice.name}</li>
                <li>· 悬停卡片可快速加入「轮播队列」或「收藏」</li>
                <li>· 在「创作中心」可自定义时钟样式与背景构图</li>
              </ul>
            </div>
          )}
        </div>

        {/* Notifications Button */}
        <div className="relative">
          <button
            type="button"
            onClick={() => {
              const next = !notifOpen;
              closeAll();
              setNotifOpen(next);
            }}
            aria-label="设备同步记录"
            className="relative flex h-9 w-9 items-center justify-center rounded-full border border-[#ECEAF6] bg-white text-[#56526B] transition-colors hover:border-[#C8BDFC] hover:text-[#5E45F8] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8]"
          >
            <Bell className="h-4 w-4" />
            {recentLogs.length > 0 && (
              <span className="absolute right-2 top-2 h-1.5 w-1.5 rounded-full bg-[#EF4444]" />
            )}
          </button>
          {notifOpen && (
            <div className="absolute right-0 top-full mt-2 w-80 rounded-2xl border border-[#ECEAF6] bg-white p-3.5 shadow-[0_18px_40px_rgba(25,15,75,0.12)]">
              <div className="mb-2 flex items-center justify-between">
                <span className="text-[13px] font-bold text-[#1A172B]">推送与设备动态</span>
                <span className="text-[11px] text-[#9A96AE]">{recentLogs.length} 条记录</span>
              </div>
              <div className="space-y-2 max-h-56 overflow-y-auto studio-scroll">
                {recentLogs.map((log, idx) => (
                  <div
                    key={idx}
                    className="rounded-xl bg-[#F7F6FC] px-3 py-2 text-[12px] text-[#4A4660]"
                  >
                    {log}
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Profile Dropdown */}
        <div className="relative">
          <button
            type="button"
            onClick={() => {
              const next = !profileOpen;
              closeAll();
              setProfileOpen(next);
            }}
            className="flex items-center gap-2 rounded-full border border-[#ECEAF6] bg-white py-1 pl-1 pr-2.5 transition-colors hover:border-[#C8BDFC] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8]"
          >
            <span className="flex h-7 w-7 items-center justify-center rounded-full bg-gradient-to-br from-[#6A52FF] to-[#B06EFF] text-white">
              <User className="h-3.5 w-3.5" />
            </span>
            <span className="hidden text-[12.5px] font-semibold text-[#1A172B] sm:inline whitespace-nowrap">
              个人中心
            </span>
            <ChevronDown className="h-3.5 w-3.5 text-[#8A86A0]" />
          </button>

          {profileOpen && (
            <div className="absolute right-0 top-full mt-2 w-52 rounded-2xl border border-[#ECEAF6] bg-white p-2 shadow-[0_18px_40px_rgba(25,15,75,0.12)]">
              <div className="border-b border-[#F1EFF8] px-3 py-2">
                <div className="text-[13px] font-bold text-[#1A172B]">Studio 创作者</div>
                <div className="text-[11px] text-[#9A96AE]">{desktopMode ? '本机工作区 · 云端未连接' : '云端空间已连接'}</div>
              </div>
              <button
                type="button"
                onClick={() => {
                  setProfileOpen(false);
                  onSelectNav('profile');
                }}
                className="mt-1 flex w-full items-center rounded-xl px-3 py-2 text-[12.5px] font-medium text-[#4A4660] hover:bg-[#F5F2FF] hover:text-[#5E45F8]"
              >
                进入个人中心
              </button>
              <button
                type="button"
                onClick={() => {
                  setProfileOpen(false);
                  onSelectNav('settings');
                }}
                className="flex w-full items-center rounded-xl px-3 py-2 text-[12.5px] font-medium text-[#4A4660] hover:bg-[#F5F2FF] hover:text-[#5E45F8]"
              >
                偏好设置
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  );
};



