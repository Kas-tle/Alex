using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Alex.API.Utils.Collections;
using Microsoft.Xna.Framework.Graphics;
using NLog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Alex.API.Graphics
{
    public class GpuResourceManager
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(GpuResourceManager));
        
        public static long GetMemoryUsage => _instance.EstMemoryUsage;
        private static GpuResourceManager _instance;
        static GpuResourceManager()
        {
            _instance = new GpuResourceManager();
        }

        public static GpuResourceManager Instance => _instance;
        
     //   private ConcurrentDictionary<long, PooledTexture2D> Textures { get; }
        //private ConcurrentDictionary<long, PooledVertexBuffer> Buffers { get; }
        //private ConcurrentDictionary<long, PooledIndexBuffer> IndexBuffers { get; }

        private long _bufferId = 0;
      //  private long _estMemoryUsage = 0;
        private long _textureId = 0;
        private long _indexBufferId = 0;
     //   private long _resourceCount = 0;
        
        public long ResourceCount => _resources.Count;
        public long EstMemoryUsage => _totalMemoryUsage;// Buffers.Values.ToArray().Sum(x => x.MemoryUsage) + Textures.Values.ToArray().Sum(x => x.MemoryUsage) + IndexBuffers.Values.ToArray().Sum(x => x.MemoryUsage);

        private long _totalMemoryUsage = 0;
        
     //   private long _textureMemoryUsage = 0;
      //  private long _indexMemoryUsage = 0;
        private Timer DisposalTimer { get; }
        private bool ShuttingDown { get; set; } = false;
        private object _disposalLock = new object();
        public GpuResourceManager()
        {
          //  Textures = new ConcurrentDictionary<long, PooledTexture2D>();
          //  Buffers = new ConcurrentDictionary<long, PooledVertexBuffer>();
         //   IndexBuffers = new ConcurrentDictionary<long, PooledIndexBuffer>();
            
            DisposalTimer = new Timer(state =>
            {
               HandleDisposeQueue();
            }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5));

            AttachCtrlcSigtermShutdown();
        }
        
        private void AttachCtrlcSigtermShutdown()
        {
            void Shutdown()
            {
                ShuttingDown = true;
            };

            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => Shutdown();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Shutdown();
                // Don't terminate the process immediately, wait for the Main thread to exit gracefully.
                eventArgs.Cancel = true;
            };
        }

        private object _resourceLock = new object();
        private List<IGpuResource> _resources = new List<IGpuResource>();
       // private ConcurrentQueue<(bool add, IGpuResource resource)> _buffer = new ConcurrentQueue<(bool add, IGpuResource resource)>();
        private List<IGpuResource> _disposalQueue = new List<IGpuResource>();
       // private SortedList<long, IGpuResource> _disposalQueue = new SortedList<long, IGpuResource>();

       private void HandleDisposeQueue()
        {
            IGpuResource[] disposed;

            lock (_disposalLock)
            {
                disposed = _disposalQueue.ToArray();
                _disposalQueue.Clear();
            }

            foreach (var dispose in disposed)
            {
                dispose.Dispose();
            }
            // while (_disposalQueue.TryDequeue(out IGpuResource resource))
            // {
            //     resource.Dispose();
            // }
        }

        public PooledVertexBuffer CreateBuffer(object caller, GraphicsDevice device, VertexDeclaration vertexDeclaration,
            int vertexCount, BufferUsage bufferUsage)
        {
           // if (GetRecycledBuffer(caller, device, vertexDeclaration, vertexCount, bufferUsage, out var b))
           // {
          //      return b;
          //  }
            
            long id = Interlocked.Increment(ref _bufferId);
            PooledVertexBuffer buffer = new PooledVertexBuffer(this, id, caller, device, vertexDeclaration, vertexCount, bufferUsage);
            buffer.Name = $"{caller.ToString()} - {id}";
            
          //  Buffers.TryAdd(id, buffer);
          _resources.Add(buffer);
            
            var size = Interlocked.Add(ref _totalMemoryUsage, buffer.MemoryUsage);
            return buffer;
        }
        
        public PooledTexture2D CreateTexture2D(object caller, GraphicsDevice graphicsDevice, int width, int height)
        {
            var id = Interlocked.Increment(ref _textureId);
            var texture = new PooledTexture2D(_instance, id, caller, graphicsDevice, width, height); 
            texture.Name = $"{caller.ToString()} - {id}";
            
            _resources.Add(texture);
          //  Textures.TryAdd(id, texture);
           // _buffer.Enqueue((true, texture));
            
          //  Interlocked.Add(ref _textureMemoryUsage, texture.Height * texture.Width * 4);
          Interlocked.Add(ref _totalMemoryUsage, texture.MemoryUsage);
            return texture;
        }
        
        public PooledTexture2D CreateTexture2D(object caller, GraphicsDevice graphicsDevice, int width, int height, bool mipmap, SurfaceFormat format)
        {
            var id = Interlocked.Increment(ref _textureId);
            var texture = new PooledTexture2D(_instance, id, caller, graphicsDevice, width, height, mipmap, format); 
            texture.Name = $"{caller.ToString()} - {id}";
            
            _resources.Add(texture);
          //  _buffer.Enqueue((true,texture));
            
          //  Textures.TryAdd(id, texture);
            
        //    Interlocked.Add(ref _textureMemoryUsage, texture.Height * texture.Width * 4);
        Interlocked.Add(ref _totalMemoryUsage, texture.MemoryUsage);
            return texture;
        }
        
        public PooledTexture2D CreateTexture2D(object caller, GraphicsDevice graphicsDevice, int width, int height, bool mipmap, SurfaceFormat format, int arraySize)
        {
            var id = Interlocked.Increment(ref _textureId);
            var texture = new PooledTexture2D(_instance, id, caller, graphicsDevice, width, height, mipmap, format, arraySize); 
            texture.Name = $"{caller.ToString()} - {id}";
            
            _resources.Add(texture);
            
           // Textures.TryAdd(id, texture);
            
        //    Interlocked.Add(ref _textureMemoryUsage, texture.Height * texture.Width * 4);
        Interlocked.Add(ref _totalMemoryUsage, texture.MemoryUsage);
            return texture;
        }

        public PooledIndexBuffer CreateIndexBuffer(object caller, GraphicsDevice graphicsDevice, IndexElementSize indexElementSize,
            int indexCount, BufferUsage bufferUsage)
        {
            var id = Interlocked.Increment(ref _indexBufferId);
            var buffer = new PooledIndexBuffer(this, id, caller, graphicsDevice, indexElementSize, indexCount, bufferUsage);
            buffer.Name = $"{caller.ToString()} - {id}";
            
            _resources.Add(buffer);
          //  _buffer.Enqueue((true,buffer));
            
         //   IndexBuffers.TryAdd(id, buffer);

            //   Interlocked.Add(ref _indexMemoryUsage, size);
         Interlocked.Add(ref _totalMemoryUsage, buffer.MemoryUsage);
            return buffer;
        }

        public static bool ReportIncorrectlyDisposedBuffers = true;
        public void Disposed(PooledVertexBuffer buffer)
        {
            if (!buffer.MarkedForDisposal && ReportIncorrectlyDisposedBuffers)
                Log.Debug($"Incorrectly disposing of buffer {buffer.PoolId}, lifetime: {DateTime.UtcNow - buffer.CreatedTime} Creator: {buffer.Owner ?? "N/A"} Memory usage: {Extensions.GetBytesReadable(buffer.MemoryUsage)}");

            //Interlocked.Add(ref _estMemoryUsage, -size);
         //   Buffers.Remove(buffer.PoolId, out _);
             //  _buffer.Enqueue((false,buffer));
             _resources.Remove(buffer);
            
            Interlocked.Add(ref _totalMemoryUsage, -buffer.MemoryUsage);
        }
        
        public void Disposed(PooledTexture2D buffer)
        {
            if (!buffer.MarkedForDisposal && ReportIncorrectlyDisposedBuffers)
                Log.Debug($"Incorrectly disposing of texture {buffer.PoolId}, lifetime: {DateTime.UtcNow - buffer.CreatedTime} Creator: {buffer.Owner ?? "N/A"} Memory usage: {Extensions.GetBytesReadable(buffer.MemoryUsage)}");

            //Interlocked.Add(ref _estMemoryUsage, -size);
           // Textures.Remove(buffer.PoolId, out _);
           //  _buffer.Enqueue((false,buffer));
          _resources.Remove(buffer);
            Interlocked.Add(ref _totalMemoryUsage, -buffer.MemoryUsage);
           // Interlocked.Add(ref _textureMemoryUsage, -(buffer.Height * buffer.Width * 4));
        }

        public void Disposed(PooledIndexBuffer buffer)
        {
            if (!buffer.MarkedForDisposal && ReportIncorrectlyDisposedBuffers)
                Log.Debug($"Incorrectly disposing of indexbuffer {buffer.PoolId}, lifetime: {DateTime.UtcNow - buffer.CreatedTime} Creator: {buffer.Owner ?? "N/A"} Memory usage: {Extensions.GetBytesReadable(buffer.MemoryUsage)}");

            //Interlocked.Add(ref _estMemoryUsage, -size);
        //    IndexBuffers.Remove(buffer.PoolId, out _);
          //  _buffer.Enqueue((false,buffer));
          _resources.Remove(buffer);
          
            Interlocked.Add(ref _totalMemoryUsage, -buffer.MemoryUsage);
           // Interlocked.Add(ref _indexMemoryUsage, -size);
        }

        internal void QueueForDisposal(IGpuResource resource)
        {
            lock (_disposalLock)
            {
                if (!_disposalQueue.Contains(resource))
                {
                    _disposalQueue.Add(resource);
                }
            }
        }
        
        //public static bool TryRecycle(object caller, GraphicsDevice device,)
        
        public static PooledVertexBuffer GetBuffer(object caller, GraphicsDevice device, VertexDeclaration vertexDeclaration,
            int vertexCount, BufferUsage bufferUsage)
        {
            return _instance.CreateBuffer(caller, device, vertexDeclaration, vertexCount, bufferUsage);
        }
        
        public static PooledTexture2D GetTexture2D(object caller, GraphicsDevice graphicsDevice, int width, int height)
        {
            return _instance.CreateTexture2D(caller, graphicsDevice, width, height);
        }
        
        public static PooledTexture2D GetTexture2D(object caller, GraphicsDevice graphicsDevice, int width, int height, bool mipmap, SurfaceFormat format)
        {
            return _instance.CreateTexture2D(caller, graphicsDevice, width, height, mipmap, format);
        }
        
        public static PooledTexture2D GetTexture2D(object caller, GraphicsDevice graphicsDevice, int width, int height, bool mipmap, SurfaceFormat format, int arraySize)
        {
            return _instance.CreateTexture2D(caller, graphicsDevice, width, height, mipmap, format, arraySize);
        }

        public static PooledTexture2D GetTexture2D(object caller, GraphicsDevice graphicsDevice, Stream stream)
        {
             //var texture = Texture2D.FromStream(graphicsDevice, stream);
             using (var texture = Image.Load<Rgba32>(stream))
             {
                 uint[] colorData;
	        
                 if (texture.TryGetSinglePixelSpan(out var pixelSpan))
                 {
                     colorData = new uint[pixelSpan.Length];

                     for (int i = 0; i < pixelSpan.Length; i++)
                     {
                         colorData[i] = pixelSpan[i].Rgba;
                     }
                 }
                 else
                 {
                     throw new Exception("Could not get image data!");
                 }

                 SurfaceFormat surfaceFormat = SurfaceFormat.Color;

                 var pooled = GetTexture2D(
                     caller, graphicsDevice, texture.Width, texture.Height, false, surfaceFormat);

                 pooled.SetData(colorData);
                 // texture.Dispose();

                 return pooled;
             }
        }

        public static PooledIndexBuffer GetIndexBuffer(object caller, GraphicsDevice graphicsDevice, IndexElementSize indexElementSize,
            int indexCount, BufferUsage bufferUsage)
        {
            return _instance.CreateIndexBuffer(caller, graphicsDevice, indexElementSize, indexCount, bufferUsage);
        }
    }
    
    public class PooledVertexBuffer : VertexBuffer, IGpuResource
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(PooledVertexBuffer));

        /// <inheritdoc />
        public EventHandler<IGpuResource> ResourceDisposed { get; set; }
        public GpuResourceManager Parent      { get; }
        public long               PoolId      { get; }
        public object             Owner       { get; private set; }
        public DateTime           CreatedTime { get; }

        public long MemoryUsage
        {
            get { return VertexDeclaration.VertexStride * VertexCount; }
        }

        public PooledVertexBuffer(GpuResourceManager parent,
            long id,
            object owner,
            GraphicsDevice graphicsDevice,
            VertexDeclaration vertexDeclaration,
            int vertexCount,
            BufferUsage bufferUsage) : base(graphicsDevice, vertexDeclaration, vertexCount, bufferUsage)
        {
            Parent = parent;
            PoolId = id;
            CreatedTime = DateTime.UtcNow;
            Owner = owner;
        }
        
        public PooledVertexBuffer(GpuResourceManager parent,
            long id,
            object owner,
            GraphicsDevice graphicsDevice,
            Type vertexType,
            int vertexCount,
            BufferUsage bufferUsage) : base(graphicsDevice, vertexType, vertexCount, bufferUsage)
        {
            Parent = parent;
            PoolId = id;
            CreatedTime = DateTime.UtcNow;
            Owner = owner;
        }
        private long _references = 0;
        public void Use(object caller)
        {
            // if (caller == Owner) return;
            
            if (Interlocked.Increment(ref _references) > 0)
            {
                
            }
        }

        public void Release(object caller)
        {
            // if (caller == Owner) return;
            
            if (Interlocked.Decrement(ref _references) == 0)
            {
                
            }
        }

        public bool MarkedForDisposal { get; private set; }
        public void ReturnResource(object caller)
        {
            if (Interlocked.Read(ref _references) > 0)
            {
                if (PooledTexture2D.ReportInvalidReturn)
                    Log.Debug(
                        $"Cannot mark vertexbuffer for disposal, has uncleared references. Owner={Owner.ToString()}, Id={PoolId}, References={_references}");

                return;
            }
            
            if (!MarkedForDisposal)
            {
                MarkedForDisposal = true;
                Parent?.QueueForDisposal(this);
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            //  if (!IsDisposed)
            if (disposing)
            {
                Parent?.Disposed(this);
                ResourceDisposed?.Invoke(this, this);
            }

            base.Dispose(disposing);
        }
    }

    public class PooledTexture2D : Texture2D, IGpuResource
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(PooledTexture2D));

        /// <inheritdoc />
        public EventHandler<IGpuResource> ResourceDisposed { get; set; }
        public GpuResourceManager Parent             { get; }
        public long               PoolId             { get; }
        public object             Owner              { get; private set; }
        public DateTime           CreatedTime        { get; }
        public bool               IsFullyTransparent { get; set; } = false;
        
        public long MemoryUsage
        {
            get { return GetFormatSize(Format) * Width * Height * LevelCount; }
        }

        private long _references = 0;

        //private WeakList<object> _objectReferences = new WeakList<object>();
        public PooledTexture2D(GpuResourceManager parent, long id, object owner, GraphicsDevice graphicsDevice, int width, int height) : base(graphicsDevice, width, height)
        {
            Parent = parent;
            PoolId = id;
            CreatedTime = DateTime.UtcNow;
            Owner = owner;
        }

        public PooledTexture2D(GpuResourceManager parent, long id, object owner, GraphicsDevice graphicsDevice, int width, int height, bool mipmap, SurfaceFormat format) : base(graphicsDevice, width, height, mipmap, format)
        {
            Parent = parent;
            PoolId = id;
            CreatedTime = DateTime.UtcNow;
            Owner = owner;
        }

        public PooledTexture2D(GpuResourceManager parent, long id, object owner, GraphicsDevice graphicsDevice, int width, int height, bool mipmap, SurfaceFormat format, int arraySize) : base(graphicsDevice, width, height, mipmap, format, arraySize)
        {
            Parent = parent;
            PoolId = id;
            CreatedTime = DateTime.UtcNow;
            Owner = owner;
        }

        private static int GetFormatSize(SurfaceFormat format)
        {
            switch (format)
            {
                case SurfaceFormat.Dxt1:
                    return 8;
                case SurfaceFormat.Dxt3:
                case SurfaceFormat.Dxt5:
                    return 16;
                case SurfaceFormat.Alpha8:
                    return 1;
                case SurfaceFormat.Bgr565:
                case SurfaceFormat.Bgra4444:
                case SurfaceFormat.Bgra5551:
                case SurfaceFormat.HalfSingle:
                case SurfaceFormat.NormalizedByte2:
                    return 2;
                case SurfaceFormat.Color:
                case SurfaceFormat.Single:
                case SurfaceFormat.Rg32:
                case SurfaceFormat.HalfVector2:
                case SurfaceFormat.NormalizedByte4:
                case SurfaceFormat.Rgba1010102:
                case SurfaceFormat.Bgra32:
                    return 4;
                case SurfaceFormat.HalfVector4:
                case SurfaceFormat.Rgba64:
                case SurfaceFormat.Vector2:
                    return 8;
                case SurfaceFormat.Vector4:
                    return 16;
                default:
                    throw new ArgumentException("Should be a value defined in SurfaceFormat", "Format");
            }
        }

        public void Use(object caller)
        {
           // if (caller == Owner) return;
            
            if (Interlocked.Increment(ref _references) > 0)
            {
                
            }
        }

        public void Release(object caller)
        {
           // if (caller == Owner) return;
            
            if (Interlocked.Decrement(ref _references) == 0)
            {
                
            }
        }
        
        /*public PooledTexture2D(GpuResourceManager parent, long id, GraphicsDevice graphicsDevice, int width, int height, bool mipmap, SurfaceFormat format, SurfaceType type, bool shared, int arraySize) : base(graphicsDevice, width, height, mipmap, format, type, shared, arraySize)
        {
            Parent = parent;
            PoolId = id;
            CreatedTime = DateTime.UtcNow;
        }*/

        public bool MarkedForDisposal { get; private set; }
        public static bool ReportInvalidReturn { get; set; } = true;

        static PooledTexture2D()
        {
            if (LogManager.Configuration.Variables.TryGetValue("textureDisposalWarning", out var v)
                && int.TryParse(v.OriginalText, out int r))
            {
                ReportInvalidReturn = r != 0;
            }
        }
        
        public void ReturnResource(object caller)
        {
            if (MarkedForDisposal) return;

            if (Interlocked.Read(ref _references) > 0)
            {
                if (ReportInvalidReturn)
                    Log.Debug(
                        $"Cannot mark texture for disposal, has uncleared references. Owner={Owner.ToString()}, Id={PoolId}, References={_references}");

                return;
            }

            MarkedForDisposal = true;
            Parent?.QueueForDisposal(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Parent?.Disposed(this);
                ResourceDisposed?.Invoke(this, this);
            }

            base.Dispose(disposing);
            
        }
    }

    public class PooledIndexBuffer : IndexBuffer, IGpuResource
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(PooledIndexBuffer));

        /// <inheritdoc />
        public EventHandler<IGpuResource> ResourceDisposed { get; set; }
        
        public GpuResourceManager Parent      { get; }
        public long               PoolId      { get; }
        public object             Owner       { get; private set; }
        public DateTime           CreatedTime { get; }

        public long MemoryUsage
        {
            get { return (IndexElementSize == IndexElementSize.SixteenBits ? 2 : 4) * IndexCount; }
        }

        public PooledIndexBuffer(GpuResourceManager parent, long id, object owner, GraphicsDevice graphicsDevice, IndexElementSize indexElementSize, int indexCount, BufferUsage bufferUsage) : base(graphicsDevice, indexElementSize, indexCount, bufferUsage)
        {
            Parent = parent;
            PoolId = id;
            CreatedTime = DateTime.UtcNow;
            Owner = owner;
        }
        
        private long _references = 0;
        public void Use(object caller)
        {
            // if (caller == Owner) return;
            
            if (Interlocked.Increment(ref _references) > 0)
            {
                
            }
        }

        public void Release(object caller)
        {
            // if (caller == Owner) return;
            
            if (Interlocked.Decrement(ref _references) == 0)
            {
                
            }
        }

        public bool MarkedForDisposal { get; private set; }
        public void ReturnResource(object caller)
        {
            if (Interlocked.Read(ref _references) > 0)
            {
                if (PooledTexture2D.ReportInvalidReturn)
                    Log.Debug(
                        $"Cannot mark indexbuffer for disposal, has uncleared references. Owner={Owner.ToString()}, Id={PoolId}, References={_references}");

                return;
            }
            
            if (!MarkedForDisposal)
            {
                MarkedForDisposal = true;
                Parent?.QueueForDisposal(this);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Parent?.Disposed(this);
                ResourceDisposed?.Invoke(this, this);
            }
            
            base.Dispose(disposing);
        }
    }

    public interface IGpuResource : IDisposable
    {
        EventHandler<IGpuResource> ResourceDisposed { get; set; }
        
        GpuResourceManager Parent { get; }
        DateTime CreatedTime { get; }
        long PoolId { get; }
        
        object Owner { get; }
        
        long MemoryUsage { get; }

        bool MarkedForDisposal { get; }
        void ReturnResource(object caller);
    }
}