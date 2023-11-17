# CelesteAnalyzer

A Roslyn analyzer which helps you avoid common mistakes made when developing Celeste mods.

## Features

### CL0001: Lambda passed to ILCursor.EmitDelegate
Passing lambdas to `ILCursor.EmitDelegate`, like this:
```csharp
cursor.EmitDelegate((int f) => f * 2);
```

Emits inefficient IL code similar to this:
```il
ldc.i4 1                                                                                                                             
ldc.i4 38750844                                                                                                                      
call T MonoMod.Utils.DynamicReferenceManager::GetValueTUnsafe<System.Delegate>(System.Int32,System.Int32)                                           
call TResult MonoMod.Cil.FastDelegateInvokers::InvokeTypeVal1<System.Int32,System.Int32>(T0,MonoMod.Cil.FastDelegateInvokers/TypeVal1`2<TResult,T0>)
```

This analyzer will warn you about this, and recommend to switch to a static method reference (though there's currently no code fixer for this):
```csharp
cursor.EmitDelegate(Manipulator);

// somewhere in your class
private static int Manipulator(int f) => f * 2;
```

This translates to a simple `call` instruction

### CL0002: Instance method passed to EmitDelegate
Avoid passing instance methods to `ILCursor.EmitDelegate`, as it can result in capturing an instance of your class in unexpected ways.
The analyzer will warn about this.

### CL0003: Orig not called in hook
Warns about not calling the `orig` method received as the 1st parameter to a On. hook in any code path, as this breaks mod compatibility.

### CL0004: Hooks should be static
Warns about non-static methods used as targets for `On` hooks or manipulators for `IL` hooks.
Provides a code fixer to make them static.