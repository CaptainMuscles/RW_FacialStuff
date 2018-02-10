﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FacialStuff.GraphicsFS;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;

namespace FacialStuff.HairCut
{
    // ReSharper disable once InconsistentNaming
    [StaticConstructorOnStartup]
    public static class CutHairDB
    {
        const string STR_MergedHair = "/Textures/MergedHair/";
        private static readonly Dictionary<GraphicRequest, Graphic> AllGraphics =
            new Dictionary<GraphicRequest, Graphic>();

        private static readonly List<HairCutPawn> PawnHairCache = new List<HairCutPawn>();

        private static Texture2D _maskTexFrontBack;

        private static Texture2D _maskTexSide;

        private static string _mergedHairPath;


        private static string MergedHairPath
        {
            get
            {
                if (!_mergedHairPath.NullOrEmpty())
                {
                    return _mergedHairPath;
                }

                ModMetaData mod = ModLister.AllInstalledMods.FirstOrDefault(
                    x => { return x?.Name != null && x.Active && x.Name.StartsWith("Facial Stuff"); });
                if (mod != null)
                {
                    _mergedHairPath = mod.RootDir + STR_MergedHair;
                }

                return _mergedHairPath;
            }
        }

        // ReSharper disable once MissingXmlDoc
        public static Graphic Get<T>(string path, Shader shader, Vector2 drawSize, Color color)
            where T : Graphic, new()
        {
            // Added second 'color' to get a separate graphic
            GraphicRequest req = new GraphicRequest(typeof(T), path, shader, drawSize, color, color, null, 0);
            return GetInner<T>(req);
        }

        public static HairCutPawn GetHairCache(Pawn pawn)
        {
            foreach (HairCutPawn c in PawnHairCache)
            {
                if (c.Pawn == pawn)
                {
                    return c;
                }
            }

            HairCutPawn n = new HairCutPawn { Pawn = pawn };
            PawnHairCache.Add(n);
            return n;
        }

        private static void CutOutHair([NotNull] ref Texture2D hairTex, Texture2D maskTex)
        {
            for (int x = 0; x < hairTex.width; x++)
            {
                for (int y = 0; y < hairTex.height; y++)
                {
                    Color maskColor = maskTex.GetPixel(x, y);
                    Color hairColor = hairTex.GetPixel(x, y);

                    Color finalColor1 = hairColor * maskColor;

                    hairTex.SetPixel(x, y, finalColor1);
                }
            }

            hairTex.Apply();
        }

        private static T GetInner<T>(GraphicRequest req)
            where T : Graphic, new()
        {
            string oldPath = req.path;
            string name = Path.GetFileNameWithoutExtension(oldPath);

            req.path = MergedHairPath + name;

            if (AllGraphics.TryGetValue(req, out Graphic graphic))
            {
                return (T)graphic;
            }
            
            // no graphics found, create =>
            graphic = Activator.CreateInstance<T>();

            // // Check if textures already present and readable, else create
            if (ContentFinder<Texture2D>.Get(req.path + "_back", false) != null)
            {
                graphic.Init(req);

                // graphic.MatFront.mainTexture = ContentFinder<Texture2D>.Get(newPath + "_front");
                // graphic.MatSide.mainTexture = ContentFinder<Texture2D>.Get(newPath + "_side");
                // graphic.MatBack.mainTexture = ContentFinder<Texture2D>.Get(newPath + "_back");
            }
            else
            {
                req.path = oldPath;
                graphic.Init(req);

                Texture2D temptexturefront = graphic.MatFront.mainTexture as Texture2D;
                Texture2D temptextureside = graphic.MatSide.mainTexture as Texture2D;
                Texture2D temptextureback = graphic.MatBack.mainTexture as Texture2D;

                Texture2D temptextureside2 = (graphic as Graphic_Multi_Four).MatLeft.mainTexture as Texture2D;

                temptexturefront = FaceTextures.MakeReadable(temptexturefront);
                temptextureside = FaceTextures.MakeReadable(temptextureside);
                temptextureback = FaceTextures.MakeReadable(temptextureback);

                temptextureside2 = FaceTextures.MakeReadable(temptextureside2);

                // intentional, only 1 mask tex. todo: rename, cleanup
                _maskTexFrontBack = FaceTextures.MaskTexAverageFrontBack;
                _maskTexSide = FaceTextures.MaskTexNarrowSide;

                CutOutHair(ref temptexturefront, _maskTexFrontBack);

                CutOutHair(ref temptextureside, _maskTexSide);

                CutOutHair(ref temptextureback, _maskTexFrontBack);

                CutOutHair(ref temptextureside2, _maskTexSide);

                req.path = MergedHairPath + name;

                // if (!name.NullOrEmpty() && !File.Exists(req.path + "_front.png"))
                // {
                // byte[] bytes = temptexturefront.EncodeToPNG();
                // File.WriteAllBytes(req.path + "_front.png", bytes);
                // byte[] bytes2 = temptextureside.EncodeToPNG();
                // File.WriteAllBytes(req.path + "_side.png", bytes2);
                // byte[] bytes3 = temptextureback.EncodeToPNG();
                // File.WriteAllBytes(req.path + "_back.png", bytes3);
                // }
                temptexturefront.Compress(true);
                temptextureside.Compress(true);
                temptextureback.Compress(true);
                temptextureside2.Compress(true);

                temptexturefront.mipMapBias = 0.5f;
                temptextureside.mipMapBias = 0.5f;
                temptextureback.mipMapBias = 0.5f;
                temptextureside2.mipMapBias = 0.5f;

                temptexturefront.Apply(false, true);
                temptextureside.Apply(false, true);
                temptextureback.Apply(false, true);
                temptextureside2.Apply(false, true);

                graphic.MatFront.mainTexture = temptexturefront;
                graphic.MatSide.mainTexture = temptextureside;
                graphic.MatBack.mainTexture = temptextureback;
                (graphic as Graphic_Multi_Four).MatLeft.mainTexture = temptextureside2;
                
                // Object.Destroy(temptexturefront);
                // Object.Destroy(temptextureside);
                // Object.Destroy(temptextureback);
            }

            AllGraphics.Add(req, graphic);

            // }
            return (T)graphic;
        }

        public static void ExportHairCut(
            HairDef hairDef,
            string name)
        {
            string path = MergedHairPath + name;
            if (!name.NullOrEmpty() && !File.Exists(path + "_front.png"))
            {
                LongEventHandler.ExecuteWhenFinished(
                    delegate
                        {
                            Graphic graphic = GraphicDatabase.Get<Graphic_Multi_Four>(hairDef.texPath, ShaderDatabase.Cutout, Vector2.one, Color.white);
                            Texture2D temptexturefront = graphic.MatFront.mainTexture as Texture2D;
                            Texture2D temptextureside = graphic.MatSide.mainTexture as Texture2D;
                            Texture2D temptextureback = graphic.MatBack.mainTexture as Texture2D;
                            Texture2D temptextureside2 = (graphic as Graphic_Multi_Four).MatLeft.mainTexture as Texture2D;
                            
                            temptexturefront = FaceTextures.MakeReadable(temptexturefront);
                            temptextureside = FaceTextures.MakeReadable(temptextureside);
                            temptextureback = FaceTextures.MakeReadable(temptextureback);
                            temptextureside2 = FaceTextures.MakeReadable(temptextureside2);

                            // intentional, only 1 mask tex. todo: rename, cleanup
                            _maskTexFrontBack = FaceTextures.MaskTexAverageFrontBack;
                            _maskTexSide = FaceTextures.MaskTexNarrowSide;

                            CutOutHair(ref temptexturefront, _maskTexFrontBack);

                            CutOutHair(ref temptextureside, _maskTexSide);

                            CutOutHair(ref temptextureback, _maskTexFrontBack);
                            CutOutHair(ref temptextureside2, _maskTexSide);



                            byte[] bytes = temptexturefront.EncodeToPNG();
                            File.WriteAllBytes(path + "_front.png", bytes);
                            byte[] bytes2 = temptextureside.EncodeToPNG();
                            File.WriteAllBytes(path + "_side.png", bytes2);
                            byte[] bytes3 = temptextureback.EncodeToPNG();
                            File.WriteAllBytes(path + "_back.png", bytes3);
                            byte[] bytes4 = temptextureside2.EncodeToPNG();
                            File.WriteAllBytes(path + "_side2.png", bytes2);


                        });
            }
        }
    }
}