// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Celeste.Mod.Helpers;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

new CelesteAnalyzer.Sample.Examples().Bad();

namespace CelesteAnalyzer.Sample
{
    public class Examples
    {

        public void Bad()
        {
            using var hook = new ILHook(() => HookTarget(420), (ctx) =>
            {
                var cursor = new ILCursor(ctx);

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate(static (int f) => f * 2);
                cursor.EmitDelegate(Cb);
                cursor.EmitDelegate(Manipulator);
                cursor.Emit(OpCodes.Starg, 0);

                Console.WriteLine(ctx.ToString());
            });

            using var detour = new Hook(() => HookTarget(420), Detour);
            using var detour2 = new Hook(() => HookTarget(420), static (Action<int> orig, int arg) =>
            {
            });
            
            
            using var detour3 = new Hook(() => HookTarget(420), static (Action<int> orig, int arg) =>
            {
                orig(arg);
            });

            RandomEvent += (orig, arg) =>
            {

            };
            On.Celeste.Player.OnNormalUpdate += static (orig, arg, arg2) =>
            {
                return orig(arg, arg2);
            };
            IL.Celeste.Player.NormalUpdate += static ctx =>
            {
                var cursor = new ILCursor(ctx);
                cursor.Remove();
                cursor.RemoveRange(4);
                cursor.TryGotoNext(MoveType.After, i => true, i => false);
                cursor.GotoNext(MoveType.After, i => true, i => false);
                cursor.GotoPrev(MoveType.After, i => true, i => false);
                cursor.TryGotoPrev(MoveType.After, i => true, i => false);
            };

            HookTarget(4);
        }

        private int Cb(int arg)
        {
            return arg * 2;
        }

        private static int Manipulator(int arg) => arg * 2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void HookTarget(int arg)
        {
            Console.WriteLine(arg);
        }

        private void Detour(Action<int> orig, int arg)
        {
            Console.WriteLine("Yo");
            orig(arg);
        }
        
        private void NotAHook(Action<int> orig, int arg)
        {
            Console.WriteLine("Yo");
        }
        
        public static event Action<Action<int>, int> RandomEvent;
    }

    [CustomEntity, Other]
    class MyEntity : Entity
    {
        public MyEntity(Vector2 blah)
        {
            var s = Scene;

            cb = () =>
            {
                Console.WriteLine(Scene);
            };
        }

        public object DoStuff()
        {
            return (Scene, Other);
        }

        private Action cb;
    }

    [CustomEntity($"MyTrigger = {nameof(Generator)}")]
    class MyTrigger : Trigger
    {
        void Generator(int a)
        {
            
        }
    }

    [Other]
    class Unrelated
    {
        
    }
}

namespace On.Celeste
{
    public class Player
    {
        public static event Func<Func<int, string, int>, int, string, int> OnNormalUpdate;
    }
}

namespace IL.Celeste
{
    public class Player
    {
        public static event Action<ILContext> NormalUpdate;
    }
}

namespace Celeste.Mod.Helpers
{
    public class CustomEntity : Attribute
    {
        public CustomEntity(params string[] ids)
        {
            
        }
    }

    public class Other : Attribute
    {
    }

    public class Entity
    {
        public object Scene { get; set; }
        
        public object Other { get; set; }
    }

    public class Trigger : Entity
    {
    }
}


