import React, { useState, useEffect } from 'react';
import {
  FileCode2,
  FolderTree,
  FileText,
  Copy,
  Check,
  Download,
  Terminal,
  Cpu,
  Layers,
  Box,
  Server,
  Zap,
  ShieldCheck,
  RefreshCw,
  Search,
  ExternalLink,
  Sparkles,
} from 'lucide-react';

interface SourceFileItem {
  path: string;
  project: string;
  fileName: string;
  extension: string;
  size: number;
}

export function DotNetFrameworkSourceExplorer() {
  const [files, setFiles] = useState<SourceFileItem[]>([]);
  const [selectedFile, setSelectedFile] = useState<string>('framework/Aspros.Base.Framework.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs');
  const [fileContent, setFileContent] = useState<string>('');
  const [loading, setLoading] = useState(false);
  const [searchFilter, setSearchFilter] = useState('');
  const [copied, setCopied] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState<'all' | 'framework' | 'samples' | 'docs'>('all');

  useEffect(() => {
    loadSourceTree();
  }, []);

  useEffect(() => {
    if (selectedFile) {
      loadFileContent(selectedFile);
    }
  }, [selectedFile]);

  const loadSourceTree = async () => {
    setLoading(true);
    try {
      const res = await fetch('/api/framework/source/tree');
      const data = await res.json();
      if (data.success && data.files) {
        setFiles(data.files);
        if (!selectedFile && data.files.length > 0) {
          setSelectedFile(data.files[0].path);
        }
      }
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  };

  const loadFileContent = async (filePath: string) => {
    try {
      const res = await fetch(`/api/framework/source/file?path=${encodeURIComponent(filePath)}`);
      const data = await res.json();
      if (data.success) {
        setFileContent(data.content);
      }
    } catch (e) {
      console.error(e);
    }
  };

  const handleCopy = () => {
    navigator.clipboard.writeText(fileContent);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const filteredFiles = files.filter((f) => {
    if (selectedCategory !== 'all' && f.project !== selectedCategory && !(selectedCategory === 'framework' && f.path.startsWith('framework'))) {
      if (selectedCategory === 'samples' && !f.path.startsWith('samples')) return false;
      if (selectedCategory === 'docs' && !f.path.startsWith('docs')) return false;
    }
    if (!searchFilter) return true;
    return f.path.toLowerCase().includes(searchFilter.toLowerCase()) || f.fileName.toLowerCase().includes(searchFilter.toLowerCase());
  });

  return (
    <div className="space-y-6">
      {/* Top Banner */}
      <div className="bg-gradient-to-r from-slate-900 via-indigo-950/40 to-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl relative overflow-hidden">
        <div className="absolute top-0 right-0 w-96 h-96 bg-indigo-500/5 rounded-full blur-3xl pointer-events-none"></div>
        <div className="relative z-10 flex flex-col lg:flex-row lg:items-center justify-between gap-6">
          <div className="space-y-2">
            <div className="flex items-center gap-2.5">
              <div className="p-2.5 rounded-xl bg-indigo-600/20 text-indigo-400 border border-indigo-500/30">
                <FileCode2 className="w-6 h-6" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h2 className="text-xl font-bold text-white tracking-tight">
                    .NET 10 (net10.0 LTS) 后端基础微服务框架源码与脚手架
                  </h2>
                  <span className="px-2 py-0.5 rounded text-[11px] font-mono bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 font-semibold">
                    C# 14 Native
                  </span>
                  <span className="px-2 py-0.5 rounded text-[11px] font-mono bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 font-semibold">
                    Native AOT Ready
                  </span>
                </div>
                <p className="text-xs text-slate-400 mt-0.5">
                  物理仓库已完整内置 <code className="text-indigo-300">Aspros.Base.Framework.sln</code>、三大分层核心类库及 Samples 示例工程。
                  支持在本地使用 Visual Studio 2026 / Rider / <code className="text-indigo-300">dotnet build</code> 直接编译执行。
                </p>
              </div>
            </div>
          </div>

          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2 text-xs bg-slate-800/80 px-3 py-1.5 rounded-lg border border-slate-700">
              <span className="w-2 h-2 rounded-full bg-emerald-400"></span>
              <span className="text-slate-300 font-mono">Solution: 6 Projects / {files.length} Files</span>
            </div>
            <button
              onClick={loadSourceTree}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium border border-slate-700 transition"
            >
              <RefreshCw className={`w-3.5 h-3.5 ${loading ? 'animate-spin' : ''}`} />
              刷新
            </button>
          </div>
        </div>

        {/* 4 Feature Badges */}
        <div className="mt-5 grid grid-cols-2 sm:grid-cols-4 gap-3 border-t border-slate-800/80 pt-4 text-xs font-mono">
          <div className="flex items-center gap-2 text-slate-300 bg-slate-950/60 p-2.5 rounded-xl border border-slate-800/80">
            <Cpu className="w-4 h-4 text-indigo-400 shrink-0" />
            <div>
              <div className="font-semibold text-slate-200">System.Threading.Lock</div>
              <div className="text-[10px] text-slate-500">C# 14 零分配作用域互斥锁</div>
            </div>
          </div>
          <div className="flex items-center gap-2 text-slate-300 bg-slate-950/60 p-2.5 rounded-xl border border-slate-800/80">
            <Layers className="w-4 h-4 text-purple-400 shrink-0" />
            <div>
              <div className="font-semibold text-slate-200">AsyncLocal WorkContext</div>
              <div className="text-[10px] text-slate-500">多租户与 Deadline 全链路透传</div>
            </div>
          </div>
          <div className="flex items-center gap-2 text-slate-300 bg-slate-950/60 p-2.5 rounded-xl border border-slate-800/80">
            <Box className="w-4 h-4 text-emerald-400 shrink-0" />
            <div>
              <div className="font-semibold text-slate-200">UnitOfWork & Dapper</div>
              <div className="text-[10px] text-slate-500">按需显式事务与连接共享</div>
            </div>
          </div>
          <div className="flex items-center gap-2 text-slate-300 bg-slate-950/60 p-2.5 rounded-xl border border-slate-800/80">
            <Zap className="w-4 h-4 text-amber-400 shrink-0" />
            <div>
              <div className="font-semibold text-slate-200">gRPC & Zone-Affinity</div>
              <div className="text-[10px] text-slate-500">机房亲和路由与级联超时</div>
            </div>
          </div>
        </div>
      </div>

      {/* Main Split Explorer */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left Column: Solution Tree */}
        <div className="lg:col-span-4 bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-sm flex flex-col h-[700px]">
          <div className="p-4 border-b border-slate-800 space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-slate-200 flex items-center gap-2">
                <FolderTree className="w-4 h-4 text-indigo-400" />
                解决方案工程树 (Solution Explorer)
              </span>
              <span className="text-[11px] font-mono text-slate-400">{filteredFiles.length} 个文件</span>
            </div>

            {/* Filter buttons */}
            <div className="flex gap-1">
              <button
                onClick={() => setSelectedCategory('all')}
                className={`px-2 py-1 rounded text-[11px] font-medium transition ${
                  selectedCategory === 'all'
                    ? 'bg-indigo-600 text-white'
                    : 'bg-slate-800/60 text-slate-400 hover:text-slate-200'
                }`}
              >
                全部
              </button>
              <button
                onClick={() => setSelectedCategory('framework')}
                className={`px-2 py-1 rounded text-[11px] font-medium transition ${
                  selectedCategory === 'framework'
                    ? 'bg-indigo-600 text-white'
                    : 'bg-slate-800/60 text-slate-400 hover:text-slate-200'
                }`}
              >
                Framework (3)
              </button>
              <button
                onClick={() => setSelectedCategory('samples')}
                className={`px-2 py-1 rounded text-[11px] font-medium transition ${
                  selectedCategory === 'samples'
                    ? 'bg-indigo-600 text-white'
                    : 'bg-slate-800/60 text-slate-400 hover:text-slate-200'
                }`}
              >
                Samples (3)
              </button>
              <button
                onClick={() => setSelectedCategory('docs')}
                className={`px-2 py-1 rounded text-[11px] font-medium transition ${
                  selectedCategory === 'docs'
                    ? 'bg-indigo-600 text-white'
                    : 'bg-slate-800/60 text-slate-400 hover:text-slate-200'
                }`}
              >
                Docs
              </button>
            </div>

            {/* Search Input */}
            <div className="relative">
              <Search className="w-3.5 h-3.5 text-slate-500 absolute left-2.5 top-2.5" />
              <input
                type="text"
                value={searchFilter}
                onChange={(e) => setSearchFilter(e.target.value)}
                placeholder="搜索 C# / proto / csproj / 类名..."
                className="w-full bg-slate-950 border border-slate-800 rounded-lg pl-8 pr-3 py-1.5 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-indigo-500"
              />
            </div>
          </div>

          {/* File list */}
          <div className="flex-1 overflow-y-auto divide-y divide-slate-800/40 p-2 space-y-0.5">
            {filteredFiles.map((f) => {
              const isSelected = selectedFile === f.path;
              return (
                <button
                  key={f.path}
                  onClick={() => setSelectedFile(f.path)}
                  className={`w-full text-left px-2.5 py-2 rounded-lg flex items-start gap-2.5 transition text-xs ${
                    isSelected
                      ? 'bg-indigo-600/20 text-indigo-200 border border-indigo-500/40'
                      : 'hover:bg-slate-800/50 text-slate-300'
                  }`}
                >
                  {f.extension === 'cs' ? (
                    <FileCode2 className={`w-4 h-4 shrink-0 mt-0.5 ${isSelected ? 'text-indigo-400' : 'text-blue-400'}`} />
                  ) : f.extension === 'proto' ? (
                    <Zap className="w-4 h-4 shrink-0 mt-0.5 text-amber-400" />
                  ) : f.extension === 'csproj' || f.extension === 'sln' ? (
                    <Box className="w-4 h-4 shrink-0 mt-0.5 text-emerald-400" />
                  ) : (
                    <FileText className="w-4 h-4 shrink-0 mt-0.5 text-slate-400" />
                  )}
                  <div className="min-w-0 flex-1">
                    <div className="font-mono text-xs font-medium truncate">{f.fileName}</div>
                    <div className="text-[10px] text-slate-500 font-mono truncate">{f.path}</div>
                  </div>
                  <span className="text-[10px] font-mono text-slate-500 shrink-0">
                    {(f.size / 1024).toFixed(1)}k
                  </span>
                </button>
              );
            })}
          </div>
        </div>

        {/* Right Column: Code Viewer */}
        <div className="lg:col-span-8 bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-sm flex flex-col h-[700px]">
          {/* Viewer Header */}
          <div className="p-4 border-b border-slate-800 flex items-center justify-between bg-slate-950/60">
            <div className="flex items-center gap-2 min-w-0">
              <FileCode2 className="w-4 h-4 text-indigo-400 shrink-0" />
              <span className="font-mono text-xs text-slate-200 font-semibold truncate">
                {selectedFile}
              </span>
              <span className="px-1.5 py-0.5 rounded text-[10px] font-mono bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">
                .NET 10 / C# 14
              </span>
            </div>

            <div className="flex items-center gap-2 shrink-0">
              <button
                onClick={handleCopy}
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium border border-slate-700 transition"
              >
                {copied ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5 text-slate-400" />}
                {copied ? '已复制' : '复制代码'}
              </button>
            </div>
          </div>

          {/* Viewer Code Area */}
          <div className="flex-1 p-4 bg-slate-950 overflow-auto font-mono text-xs text-slate-200 leading-relaxed selection:bg-indigo-600 selection:text-white">
            <pre className="whitespace-pre">{fileContent || '// 正在读取源文件内容...'}</pre>
          </div>

          {/* Viewer Footer */}
          <div className="p-3 border-t border-slate-800 bg-slate-900/90 text-xs text-slate-400 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <ShieldCheck className="w-3.5 h-3.5 text-emerald-400" />
              <span>物理源文件已持久化至根目录与 <code className="text-slate-300 font-mono">framework/</code>、<code className="text-slate-300 font-mono">samples/</code></span>
            </div>
            <div className="font-mono text-[11px] text-slate-500">
              UTF-8 | {fileContent ? fileContent.split('\n').length : 0} 行 | {(new Blob([fileContent || '']).size / 1024).toFixed(1)} KB
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
