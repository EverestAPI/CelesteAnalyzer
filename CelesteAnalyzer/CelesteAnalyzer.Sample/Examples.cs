// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using System;
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
            using var detour2 = new Hook(() => HookTarget(420), (Action<int> orig, int arg) =>
            {
            });
            
            
            using var detour3 = new Hook(() => HookTarget(420), static (Action<int> orig, int arg) =>
            {
                orig(arg);
            });

            RandomEvent += (orig, arg) =>
            {

            };
            On.Celeste.Player.OnNormalUpdate += (orig, arg) =>
            {
            };
            IL.Celeste.Player.NormalUpdate += ctx =>
            {
                var cursor = new ILCursor(ctx);
            };

            HookTarget(4);
        }

        private int Cb(int arg)
        {
            return arg * 2;
        }

        private static int Manipulator(int arg) => arg * 2;

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
}

namespace On.Celeste
{
    public class Player
    {
        public static event Action<Action<int>, int> OnNormalUpdate;
    }
}

namespace IL.Celeste
{
    public class Player
    {
        public static event Action<ILContext> NormalUpdate;
    }
}


