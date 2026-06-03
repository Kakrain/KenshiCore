using KenshiCore.Mods;
using KenshiCore.Utilities;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace KenshiCore.OgreEngineering
{
    public enum ChunkType : ushort
    {
        BLEND = 0x1010,
        BONE = 0x2000,
        BONE_PARENT = 0x3000,
        ANIMATION = 0x4000,
        ANIMATION_TRACK_KEYFRAME = 0x4110,
        ANIMATION_TRACK = 0x4100,
        ANIMATION_LINK= 0x5000
    }
    
    public enum endianType : ushort
    {
        NORMAL = 0x1000,
        OTHER = 0x0010
    }
    

    
    public class SkeletonEngineer
        {
            private string skeletonName = "";
            private SkeletonHeader header;
            const uint KEYFRAMESIZEWITHOUTSCALE = 38;
            private List<SkeletonChunk> chunks = new();
            private OgreContext context;
            public SkeletonEngineer()
            {
                header = new SkeletonHeader();
                context = null!;
            }
        private List<string>? readChunkOnlyBones(bool alreadyReadBones)
        {
            var (id, length) = context.ReadChunkHeader();
            List <string> bones= new List<string>();
            SkeletonChunk? chunk = null;
            switch (id)
            {
                case (int)ChunkType.BLEND:
                    chunk = new BlendMode(context);
                    break;
                case (int)ChunkType.BONE:
                    chunk = new Bone(context);
                    alreadyReadBones = true;
                    break;
                case (int)ChunkType.BONE_PARENT:
                    chunk = new BoneParent(context);
                    if (alreadyReadBones)
                    {
                        return null;
                    }
                    break;
                case (int)ChunkType.ANIMATION:
                    chunk = new Animation(context); 
                    if (alreadyReadBones)
                    {
                        return null;
                    }
                    break;
                case (int)ChunkType.ANIMATION_LINK:
                    chunk = new AnimationLink(context);
                    if (alreadyReadBones)
                    {
                        return null;
                    }
                    break;
                default:
                    Logger.Print($"Unknown chunk ID: 0x{id:X4}");
                    throw new Exception($"Unknown chunk ID: 0x{id:X4}");
            }
            if (chunk != null)
            {
                chunk.Read(length);
                if(chunk is Bone)
                    bones.Add(((Bone)chunk).name);
                chunks.Add(chunk);
            }
            return bones;
        }
        private void readChunk()
            {
                var (id, length) = context.ReadChunkHeader();
                Logger.Print("id" + id.ToString("X4"));
                Logger.Print("mCurrentstreamLen" + length.ToString("X4"));

                SkeletonChunk? chunk = null;
                switch (id)
                {
                    case (int)ChunkType.BLEND:
                        chunk = new BlendMode(context);
                        break;
                    case (int)ChunkType.BONE:
                        chunk = new Bone(context);
                        break;
                    case (int)ChunkType.BONE_PARENT:
                        chunk = new BoneParent(context);
                        break;
                    case (int)ChunkType.ANIMATION:
                        chunk = new Animation(context);
                        break;
                    case (int)ChunkType.ANIMATION_LINK:
                        chunk = new AnimationLink(context);
                        break;
                    default:
                        Logger.Print($"Unknown chunk ID: 0x{id:X4}");
                        throw new Exception($"Unknown chunk ID: 0x{id:X4}");
                }
                if (chunk != null)
                {
                    chunk.Read(length);
                    chunks.Add(chunk);
                }
            }
            public string getSkeletonFingerPrint(string path)
            {
                using var fs = File.OpenRead(path);
                context = new OgreContext(new BinaryReader(fs, Encoding.UTF8));
                string fileName = Path.GetFileName(path);
                string extension = Path.GetExtension(fileName).ToLowerInvariant();
                this.skeletonName = fileName;
                List<string> bones = new List<string>();
                this.header = ParseHeader();

                while (!context.IsEndOfStream(6))
                {
                    if (context.IsEndOfStream(6))
                        return "";
                    List<string>? newBones = readChunkOnlyBones(bones.Count > 0);
                    if(newBones == null)
                    {
                        return string.Join("|", bones);//.OrderBy(x => x)
                }
                    bones.AddRange(newBones);
                }
                return string.Join("|", bones);//.OrderBy(x => x)
        }
        public void LoadSkeletonFile(string path)
            {
                using var fs = File.OpenRead(path);
                context = new OgreContext(new BinaryReader(fs, Encoding.UTF8));
                string fileName = Path.GetFileName(path);
                string extension = Path.GetExtension(fileName).ToLowerInvariant();
                this.skeletonName = fileName;

                this.header = ParseHeader();

                while (!context.IsEndOfStream(6))
                {
                    if (context.IsEndOfStream(6))
                        return;
                    readChunk();
                }
            }
        public static bool AreSkeletonsCompatible(string BaseFingerprint, string targetFingerprint)
            {
                var animationBones = targetFingerprint
                    .Split('|', StringSplitOptions.RemoveEmptyEntries)
                    .ToHashSet();

                var raceBones = BaseFingerprint
                    .Split('|', StringSplitOptions.RemoveEmptyEntries)
                    .ToHashSet();

                return animationBones.IsSubsetOf(raceBones);
            }
        private SkeletonHeader ParseHeader()
            {
                var header = new SkeletonHeader();
                context.loadFlipEndian();
                string version = context.ReadString();

                Logger.Print($"Version: {version}");
                return header;
            }
            public class SkeletonData
            {
                public SkeletonHeader? Header { get; set; }
                public byte[]? Leftover { get; set; }
            }
            public class SkeletonHeader
            {
            }
            public abstract class SkeletonChunk
            {
                protected OgreContext context;
                public abstract void Read(uint mCurrentstreamLen);
                protected SkeletonChunk(OgreContext context)
                {
                    this.context = context;
                }
            }
            public class Bone : SkeletonChunk
            {
                const uint BONESIZEWITHOUTSCALE = 36;
                public string name = "";
                uint handle = 0;
                float[] vector = new float[3];
                float[] quaternion = new float[4];
                float[] scale = new float[3] { 1.0f, 1.0f, 1.0f };
                public Bone(OgreContext context) : base(context) { }
                public override void Read(uint mCurrentstreamLen)
                {
                    name = context.ReadString();
                    Logger.Print($"Bone: {name}");
                    handle = context.ReadUInt16();
                    Logger.Print($"handle: {handle}");
                    vector = context.ReadFloats(3);
                    Logger.Print($"vector: {string.Join(", ", vector)}");
                    quaternion = context.ReadFloats(4);
                    Logger.Print($"quaternion: {string.Join(", ", quaternion)}");
                    if (mCurrentstreamLen > BONESIZEWITHOUTSCALE)
                    {
                        scale = context.ReadFloats(3);
                        Logger.Print($"scale: {string.Join(", ", scale)}");
                    }
                }
            }
            public class Animation : SkeletonChunk
            {
                String name = "";
                public Animation(OgreContext context) : base(context) { }
                public override void Read(uint mCurrentstreamLen)
                {
                    name = context.ReadString();
                    Logger.Print($"Animation name: {name}");
                    float len = context.ReadFloat();
                    Logger.Print($"Animation length: {len}");

                    var (id, length) = context.ReadChunkHeader();
                    Logger.Print("id" + id.ToString("X4"));
                    Logger.Print("mCurrentstreamLen" + length.ToString("X4"));


                    while (id == (int)ChunkType.ANIMATION_TRACK)
                    {
                        AnimationTrack atrack = new AnimationTrack(context);
                        atrack.Read(length);
                        if (context.IsEndOfStream(6))
                            return;
                        (id, length) = context.ReadChunkHeader();
                    }
                    context.offsetStream(-6);
                }
            }
            public class BoneParent : SkeletonChunk
            {
                ushort childHandle = 0;
                ushort parentHandle = 0;
                public BoneParent(OgreContext context) : base(context) { }
                public override void Read(uint mCurrentstreamLen)
                {
                    childHandle = context.ReadUInt16();
                    parentHandle = context.ReadUInt16();
                    Logger.Print($"childHandle: {childHandle}");
                    Logger.Print($"parentHandle: {parentHandle}");
                }


            }
            public class AnimationLink : SkeletonChunk
            {
                public string name = "";
                public AnimationLink(OgreContext context) : base(context) { }
                public override void Read(uint mCurrentstreamLen)
                {
                    name = context.ReadString();
                    Logger.Print($"Bone Animation Link: {name}");
                }
            }
            public class BlendMode : SkeletonChunk
            {
                uint blendMode = 0;
                public BlendMode(OgreContext context) : base(context) { }
                public override void Read(uint mCurrentstreamLen)
                {
                    blendMode = context.ReadUInt16();
                    Logger.Print($"Blendmode: {blendMode}");
                }
            }

            public class AnimationTrack : SkeletonChunk
            {
                uint boneHandle = 0;
                public AnimationTrack(OgreContext context) : base(context) { }
                public override void Read(uint mCurrentstreamLen)
                {
                    boneHandle = context.ReadUInt16();
                    Logger.Print($"BoneHandle AnimationTrack: {boneHandle}");
                    var (id, length) = context.ReadChunkHeader();
                    while (id == (int)ChunkType.ANIMATION_TRACK_KEYFRAME)
                    {

                        KeyFrame keyf = new KeyFrame(context);
                        keyf.Read(length);
                        if (context.IsEndOfStream(6))
                            return;
                        (id, length) = context.ReadChunkHeader();
                    }
                    context.offsetStream(-6);
                }
            }

            public class KeyFrame : SkeletonChunk
            {
                float time = 0.0f;
                public KeyFrame(OgreContext context) : base(context) { }
                public override void Read(uint mCurrentstreamLen)
                {
                    time = context.ReadFloat();

                    float[] quaternion_rot = context.ReadFloats(4);
                    Logger.Print($"quaternion_rot: {string.Join(", ", quaternion_rot)}");

                    float[] vector_trans = context.ReadFloats(3);
                    Logger.Print($"vector_trans: {string.Join(", ", vector_trans)}");

                    if (mCurrentstreamLen > KEYFRAMESIZEWITHOUTSCALE)
                    {
                        float[] vector_scale = context.ReadFloats(3);
                        Logger.Print($"vector_scale: {string.Join(", ", vector_scale)}");
                    }
                }
            }
        }
    }

