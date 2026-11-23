import React from 'react';
import {
  Home,
  Cloud,
  FolderOpen,
  PenTool,
  ListVideo,
  Clock,
  Puzzle,
  Monitor,
  Settings,
  ArrowRight,
  Layers,
} from 'lucide-react';
import { DeviceInfo, NavSection } from '../data/themes';

interface SidebarProps {
  activeNav: NavSection;
  onSelectNav: (nav: NavSection) => void;
  activeDevice: DeviceInfo;
  isPlaying: boolean;
}

interface NavItemConfig {
  id: NavSection;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
}

const NAV_GROUPS: { title?: string; items: NavItemConfig[] }[] = [
  {
    items: [{ id: 'home', label: '首页', icon: Home }],
  },
  {
    title: '发现',
    items: [{ id: 'online', label: '在线资源', icon: Cloud }],
  },
  {
    title: '我的',
    items: [
      { id: 'library', label: '资源库', icon: FolderOpen },
      { id: 'creator', label: '创作中心', icon: PenTool },
      { id: 'playlist', label: '轮播列表', icon: ListVideo },
      { id: 'schedule', label: '定时切换', icon: Clock },
      { id: 'plugins', label: '插件中心', icon: Puzzle },
    ],
  },
  {
    title: '设备与偏好',
    items: [
      { id: 'devices', label: '设备管理', icon: Monitor },
      { id: 'settings', label: '设置', icon: Settings },
    ],
  },
];

export const Sidebar: React.FC<SidebarProps> = ({
  activeNav,
  onSelectNav,
  activeDevice,
  isPlaying,
}) => {
  return (
    <aside className="flex h-full w-[180px] shrink-0 flex-col justify-between overflow-y-auto border-r border-[#ECEAF6] bg-white/88 px-3.5 py-5 backdrop-blur-xl studio-scroll max-lg:hidden">
      <div>
        {/* Brand Lockup matching Figure 2 */}
        <button
          type="button"
          onClick={() => onSelectNav('home')}
          className="mb-6 flex items-center gap-2.5 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8] rounded-xl px-1 py-1"
        >
          <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-[#9C51FF] via-[#6A4BFF] to-[#4E35E8] text-white shadow-[0_8px_20px_rgba(94,69,248,0.34)]">
            <Layers className="h-4 w-4" />
          </div>
          <span className="text-[17px] font-extrabold tracking-tight text-[#1A172B] whitespace-nowrap">
            D Studio
          </span>
        </button>

        {/* Navigation Groups */}
        <nav className="space-y-3.5" aria-label="主导航">
          {NAV_GROUPS.map((group, gIdx) => (
            <div key={group.title ?? `group-${gIdx}`}>
              {group.title && (
                <div className="mb-1.5 mt-3.5 border-t border-[#F1EFF8] pt-3 text-[11px] font-semibold tracking-wide text-[#9A96AE]">
                  {group.title}
                </div>
              )}
              <ul className="space-y-1">
                {group.items.map((item) => {
                  const Icon = item.icon;
                  const isActive = activeNav === item.id;
                  return (
                    <li key={item.id} className="relative">
                      <button
                        type="button"
                        onClick={() => onSelectNav(item.id)}
                        className={`group relative flex w-full items-center gap-2.5 rounded-xl py-1.5 pr-1 text-left transition-all duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8] ${
                          isActive
                            ? 'text-[#1A172B] font-semibold'
                            : 'text-[#6B6880] font-medium hover:text-[#1A172B]'
                        }`}
                      >
                        <span
                          className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-xl transition-all duration-150 ${
                            isActive
                              ? 'bg-gradient-to-br from-[#6B53FF] to-[#563CEB] text-white shadow-[0_8px_18px_rgba(94,69,248,0.32)]'
                              : 'border border-[#EBE9F5] bg-white text-[#7A7690] group-hover:border-[#D6CFF9] group-hover:text-[#5E45F8]'
                          }`}
                        >
                          <Icon className="h-[15px] w-[15px]" />
                        </span>
                        <span className="text-[13px] whitespace-nowrap truncate">
                          {item.label}
                        </span>
                      </button>
                      {/* Right-edge active bar inspired by Figure 2 */}
                      {isActive && (
                        <span
                          aria-hidden="true"
                          className="absolute -right-3.5 top-1/2 h-6 w-[3.5px] -translate-y-1/2 rounded-l-full bg-[#5E45F8]"
                        />
                      )}
                    </li>
                  );
                })}
              </ul>
            </div>
          ))}
        </nav>
      </div>

      {/* Bottom Cosmic Aurora Hardware Card inspired by Figure 2's Join Premium card */}
      <div className="mt-5">
        <div className="aurora-dark-card relative overflow-hidden rounded-[20px] p-3.5 text-white shadow-[0_18px_36px_rgba(24,9,66,0.28)]">
          <div className="relative z-10">
            <div className="truncate text-[13px] font-bold tracking-tight text-white">
              {activeDevice.name}
            </div>
            <div className="mt-0.5 truncate font-mono-tabular text-[10.5px] text-white/70">
              {activeDevice.resolution} · {isPlaying ? '输出中' : '已暂停'}
            </div>

            {/* Isometric 3D Prism Graphic matching Figure 2 */}
            <div className="my-2.5 flex items-center justify-center">
              <svg
                viewBox="0 0 160 86"
                className="h-[58px] w-full"
                aria-hidden="true"
              >
                <defs>
                  <linearGradient id="sideBarCubeTop" x1="0" y1="0" x2="1" y2="1">
                    <stop offset="0%" stopColor="#F5D0FE" />
                    <stop offset="100%" stopColor="#8B5CF6" />
                  </linearGradient>
                  <linearGradient id="sideBarCubeLeft" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#7C5CFF" />
                    <stop offset="100%" stopColor="#3B1D8E" />
                  </linearGradient>
                  <linearGradient id="sideBarCubeRight" x1="0" y1="0" x2="1" y2="1">
                    <stop offset="0%" stopColor="#C084FC" />
                    <stop offset="100%" stopColor="#5E45F8" />
                  </linearGradient>
                </defs>
                <path
                  d="M20 46 L42 32 L54 38 L78 20"
                  fill="none"
                  stroke="#A78BFA"
                  strokeWidth="1.6"
                  strokeLinecap="round"
                  opacity="0.7"
                />
                <path
                  d="M72 20 L78 20 L77 26"
                  fill="none"
                  stroke="#A78BFA"
                  strokeWidth="1.6"
                  strokeLinecap="round"
                  opacity="0.7"
                />
                {/* Small left block */}
                <polygon points="56,52 74,42 92,52 74,62" fill="url(#sideBarCubeTop)" />
                <polygon points="56,52 74,62 74,82 56,72" fill="url(#sideBarCubeLeft)" />
                <polygon points="92,52 74,62 74,82 92,72" fill="url(#sideBarCubeRight)" />
                {/* Tall right block */}
                <polygon points="90,34 114,21 138,34 114,47" fill="url(#sideBarCubeTop)" />
                <polygon points="90,34 114,47 114,82 90,69" fill="url(#sideBarCubeLeft)" />
                <polygon points="138,34 114,47 114,82 138,69" fill="url(#sideBarCubeRight)" />
              </svg>
            </div>

            <button
              type="button"
              onClick={() => onSelectNav('devices')}
              className="flex w-full items-center justify-between rounded-full border border-white/15 bg-black/35 px-3 py-1.5 text-[11.5px] font-semibold text-white backdrop-blur-md transition-all duration-150 hover:bg-white/15 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white/60 whitespace-nowrap"
            >
              <span className="flex items-center gap-1.5">
                <span className="flex h-4 w-4 items-center justify-center rounded-full bg-[#5E45F8] text-white">
                  <Monitor className="h-2.5 w-2.5" />
                </span>
                管理设备
              </span>
              <ArrowRight className="h-3 w-3 text-white/80" />
            </button>
          </div>
        </div>
      </div>
    </aside>
  );
};
