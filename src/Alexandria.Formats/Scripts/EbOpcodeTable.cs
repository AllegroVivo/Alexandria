namespace Alexandria.Formats.Scripts;

/// <summary>
/// GENERATED from the reference's EventEngine.DoEventCode: for each opcode, the argument
/// widths read at the TOP LEVEL of its case block - getv1/getv2/getv3 (and getv3u, the
/// unsigned 3-byte variant), plus GetObj1/GetObj3, which are just GetObjUID(getvN()).
/// That is all the disassembler needs to step over an instruction it does not model.
/// 
/// Deliberately absent: opcodes whose case shares a label with another, branches on
/// eventCodeBinary, loops, reads a raw byte with geti(), or reads an argument inside a
/// nested if. Those cannot be expressed as a fixed width list and need hand-written cases
/// in EbDisassembler, which also takes precedence over this table for anything it names.
/// 
/// 214 opcodes here; 47 held back as hand-only.
/// </summary>
public static class EbOpcodeTable
{
    public static readonly IReadOnlyDictionary<Int32, (String Name, Int32[] ArgWidths)> Generated =
        new Dictionary<Int32, (String, Int32[])>
        {
            [0x00] = ("NOTHING", []),                     // NOP
            [0x1B] = ("ContinueBattleMusic", [1]),        // SONGFLAG
            [0x1E] = ("SetCameraBounds", [1, 2, 2, 2, 2]),// BGVPORT
            [0x1F] = ("WindowSync", [1, 1, 2]),           // MES
            [0x20] = ("WindowAsync", [1, 1, 2]),          // MESN
            [0x21] = ("CloseWindow", [1]),                // CLOSE
            [0x23] = ("Walk", [2, 2]),                    // MOVE
            [0x24] = ("WalkTowardObject", [1]),           // MOVA
            [0x25] = ("InitWalk", []),                    // CLRDIST
            [0x26] = ("SetWalkSpeed", [1]),               // MSPEED
            [0x27] = ("SetTriangleFlagMask", [1]),        // BGIMASK
            [0x28] = ("Cinematic", [1, 2, 1]),            // FMV
            [0x2A] = ("Battle", [1, 2]),                  // ENCOUNT
            [0x2B] = ("Field", [2]),                      // MAPJUMP
            [0x2C] = ("DefinePlayerCharacter", []),       // CC
            [0x2D] = ("DisableMove", []),                 // UCOFF
            [0x2E] = ("EnableMove", []),                  // UCON
            [0x33] = ("SetStandAnimation", [2]),          // AIDLE
            [0x34] = ("SetWalkAnimation", [2]),           // AWALK
            [0x35] = ("SetWalkAnimation", [2]),           // ARUN
            [0x37] = ("SetPitchAngle", [1, 1]),           // ROTXZ
            [0x39] = ("ShowObject", [1, 1]),              // MESHSHOW
            [0x3A] = ("HideObject", [1, 1]),              // MESHHIDE
            [0x3B] = ("SetObjectIndex", [1]),             // OBJINDEX
            [0x3C] = ("SetRandomBattles", [1, 2, 2, 2, 2]),// ENCSCENE
            [0x3D] = ("SetAnimationInOut", [1, 1]),       // AFRAME
            [0x3F] = ("SetAnimationFlags", [1, 1]),       // AMODE
            [0x41] = ("WaitAnimation", []),               // WAITANIM
            [0x42] = ("StopAnimation", []),               // ENDANIM
            [0x43] = ("RunSharedScript", [1]),            // STARTSEQ
            [0x44] = ("WaitSharedScript", []),            // WAITSEQ
            [0x45] = ("StopSharedScript", []),            // ENDSEQ
            [0x47] = ("EnableHeadFocus", [1]),            // NECKFLAG
            [0x48] = ("AddItem", [2, 1]),                 // ITEMADD
            [0x49] = ("RemoveItem", [2, 1]),              // ITEMDELETE
            [0x4A] = ("RunBattleCode", [1, 2]),           // BTLSET
            [0x4B] = ("SetObjectLogicalSize", [1, 1, 1]), // RADIUS
            [0x4C] = ("AttachObject", [1, 1, 1]),         // ATTACH
            [0x4D] = ("DetachObject", [1]),               // DETACH
            [0x4F] = ("0x4F", []),                        // STOP
            [0x51] = ("TurnTowardObject", [1, 1]),        // TURNA
            [0x52] = ("SetInactiveAnimation", [2]),       // ASLEEP
            [0x53] = ("PreventWindowInit", []),           // NOINITMES
            [0x55] = ("SetWalkTurnSpeed", [1]),           // MROT
            [0x57] = ("SetRandomBattleFrequency", [1]),   // ENCRATE
            [0x58] = ("BGSMOVE", [2, 2, 2]),              // BGSMOVE
            [0x59] = ("SetTileColor", [1, 1, 1, 1]),      // BGLCOLOR
            [0x5A] = ("SetTilePositionEx", [1, 2, 2, 2]), // BGLMOVE
            [0x5B] = ("ShowTile", [1, 1]),                // BGLACTIVE
            [0x5C] = ("MoveTileLoop", [1, 1, 2, 2]),      // BGLLOOP
            [0x5D] = ("MoveTile", [1, 1, 2, 2]),          // BGLPARALLAX
            [0x5E] = ("SetTilePosition", [1, 2, 2]),      // BGLORIGIN
            [0x5F] = ("RunTileAnimation", [1, 1]),        // BGAANIME
            [0x60] = ("ActivateTileAnimation", [1, 1]),   // BGAACTIVE
            [0x61] = ("SetTileAnimationSpeed", [1, 2]),   // BGARATE
            [0x62] = ("SetRow", [1, 1]),                  // SETROW
            [0x63] = ("SetTileAnimationPause", [1, 1, 1]),// BGAWAIT
            [0x64] = ("SetTileAnimationFlags", [1, 1]),   // BGAFLAG
            [0x65] = ("RunTileAnimationEx", [1, 1, 1]),   // BGARANGE
            [0x66] = ("SetTextVariable", [1, 2]),         // MESVALUE
            [0x67] = ("SetControlDirection", [1, 1]),     // TWIST
            [0x68] = ("Bubble", [1]),                     // FICON
            [0x69] = ("ChangeTimerTime", [2]),            // TIMERSET
            [0x6A] = ("DisableRun", []),                  // DASHOFF
            [0x6B] = ("SetBackgroundColor", [1, 1, 1]),   // CLEARCOLOR
            [0x6F] = ("MoveCamera", [2, 2, 1, 1]),        // BGSSCROLL
            [0x70] = ("ReleaseCamera", [1, 1]),           // BGSRELEASE
            [0x71] = ("EnableCameraServices", [1, 1, 1]), // BGCACTIVE
            [0x72] = ("SetCameraFollowHeight", [2]),      // BGCHEIGHT
            [0x73] = ("EnableCameraFollow", []),          // BGCLOCK
            [0x74] = ("DisableCameraFollow", []),         // BGCUNLOCK
            [0x75] = ("Menu", [1, 1]),                    // MENU
            [0x76] = ("DrawRegionStart", [2, 2]),         // TRACKSTART
            [0x77] = ("DrawRegionSetLast", [2, 2]),       // TRACK
            [0x78] = ("DrawRegionPushNew", []),           // TRACKADD
            [0x79] = ("0x79", []),                        // PRINTQUAD
            [0x7A] = ("SetLeftAnimation", [2]),           // ATURNL
            [0x7B] = ("SetRightAnimation", [2]),          // ATURNR
            [0x7C] = ("EnableDialogChoices", [2, 1]),     // CHOOSEPARAM
            [0x7D] = ("RunTimer", [1]),                   // TIMERCONTROL
            [0x7E] = ("SetFieldCamera", [1]),             // SETCAM
            [0x7F] = ("EnableShadow", []),                // SHADOWON
            [0x80] = ("DisableShadow", []),               // SHADOWOFF
            [0x81] = ("SetShadowSize", [1, 1]),           // SHADOWSCALE
            [0x82] = ("SetShadowOffset", [2, 2]),         // SHADOWOFFSET
            [0x83] = ("LockShadowRotation", [1]),         // SHADOWLOCK
            [0x84] = ("UnlockShadowRotation", []),        // SHADOWUNLOCK
            [0x85] = ("SetShadowAmplifier", [1]),         // SHADOWAMP
            [0x86] = ("SetAnimationStandSpeed", [1, 1, 1, 1]),// IDLESPEED
            [0x87] = ("TurnInstantEx", [1, 1]),           // DDIR
            [0x88] = ("RunModelCode", [1, 2, 2, 2]),      // CHRFX
            [0x89] = ("SetSoundPosition", [2, 2, 2, 1]),  // SEPV
            [0x8A] = ("SetSoundObjectPosition", [1, 1]),  // SEPVA
            [0x8B] = ("SetHeadFocusMask", [1, 1]),        // NECKID
            [0x8C] = ("BattleEx", [1, 1, 2]),             // ENCOUNT2
            [0x8D] = ("ShowTimer", [1]),                  // TIMERDISPLAY
            [0x8E] = ("RaiseWindows", []),                // RAISE
            [0x8F] = ("SetModelColor", [1, 1, 1, 1]),     // CHRCOLOR
            [0x90] = ("DisableInactiveAnimation", []),    // SLEEPINH
            [0x91] = ("FollowFocus", [1]),                // AUTOTURN
            [0x92] = ("AttachTile", [1, 2, 1, 1, 1, 1, 1]),// BGLATTACH
            [0x93] = ("SetObjectFlags", [1]),             // CFLAG
            [0x94] = ("SetJumpAnimation", [2, 1, 1]),     // AJUMP
            [0x96] = ("WindowAsyncEx", [1, 1, 1, 2]),     // MESAN
            [0x97] = ("ReturnEntryFunctions", [1]),       // DRET
            [0x98] = ("MakeAnimationLoop", [1]),          // MOVT
            [0x99] = ("SetTurnSpeed", [1]),               // TSPEED
            [0x9A] = ("EnablePathTriangle", [2, 1]),      // BGIACTIVET
            [0x9B] = ("TurnTowardPosition", [2, 2]),      // TURNTO
            [0x9C] = ("RunJumpAnimation", []),            // PREJUMP
            [0x9D] = ("RunLandAnimation", []),            // POSTJUMP
            [0x9F] = ("SetObjectSize", [1, 1, 1, 1]),     // CHRSCALE
            [0xA0] = ("WalkToExit", []),                  // MOVJ
            [0xA2] = ("WalkXZY", [2, 2, 2]),              // MOVE3
            [0xA3] = ("DRADIUS", [1, 1, 1, 1]),           // DRADIUS
            [0xA4] = ("CalculateExitPosition", []),       // MJPOS
            [0xA5] = ("Slide", [2, 2]),                   // MOVH
            [0xA6] = ("SetRunSpeedLimit", [1]),           // SPEEDTH
            [0xA7] = ("Turn", [1]),                       // TURNDS
            [0xA8] = ("SetPathing", [1]),                 // BGI
            [0xA9] = ("CalculateScreenPosition", [1]),    // GETSCREEN
            [0xAA] = ("EnableMenu", []),                  // MENUON
            [0xAB] = ("DisableMenu", []),                 // MENUOFF
            [0xAC] = ("ChangeDisc", [2]),                 // DISCCHANGE
            [0xAD] = ("MoveInstantXZYEx", [1, 2, 2, 2]),  // DPOS3
            [0xAE] = ("TetraMaster", [2]),                // MINIGAME
            [0xAF] = ("DeleteAllCards", []),              // DELETEALLCARD
            [0xB0] = ("SetFieldName", [2]),               // SETMAPNAME
            [0xB1] = ("ResetFieldName", []),              // RESETMAPNAME
            [0xB4] = ("SetPartyReserve", [2]),            // FULLMEMBER
            [0xB5] = ("PretendToBe", [1]),                // PRETEND
            [0xB6] = ("WorldMap", [2]),                   // WMAPJUMP
            [0xB7] = ("0xB7", []),                        // EYE
            [0xB8] = ("0xB8", []),                        // AIM
            [0xB9] = ("AddControllerMask", [1, 2]),       // SETKEYMASK
            [0xBA] = ("RemoveControllerMask", [1, 2]),    // CLEARKEYMASK
            [0xBB] = ("TimedTurnEx", [1, 1, 1]),          // DTURN
            [0xBC] = ("WaitTurnEx", [1]),                 // DWAITTURN
            [0xBD] = ("RunAnimationEx", [1, 2]),          // DANIM
            [0xBE] = ("WaitAnimationEx", [1]),            // DWAITANIM
            [0xBF] = ("MoveInstantEx", [1, 2, 2]),        // DPOS
            [0xC2] = ("StopTextureAnimation", [1, 1]),    // TEXSTOP
            [0xC3] = ("SetTileCamera", [1, 1]),           // BGVSET
            [0xC4] = ("RunWorldCode", [1, 2]),            // WPRM
            [0xC8] = ("RunSoundCode3", [2, 2, 3, 1, 1]),  // FLDSND3
            [0xC9] = ("SetupTileLoopingWindow", [1, 2, 2, 2, 2]),// BGVDEFINE
            [0xCA] = ("ResetTileAnimation", [1, 1]),      // BGAVISIBLE
            [0xCB] = ("EnablePath", [1, 1]),              // BGIACTIVEF
            [0xCC] = ("AddCharacterAttribute", [2]),      // CHRSET
            [0xCD] = ("RemoveCharacterAttribute", [2]),   // CHRCLEAR
            [0xCE] = ("AddGi", [3]),                      // GILADD
            [0xCF] = ("RemoveGi", [3]),                   // GILDELETE
            [0xD0] = ("BattleDialog", [2]),               // MESB
            [0xD3] = ("DEBUGLOAD", []),                   // DEBUGLOAD
            [0xD4] = ("AttachObjectOffset", [2, 2, 2]),   // ATTACHOFFSET
            [0xD7] = ("ATE", [1]),                        // AICON
            [0xD8] = ("SetWeather", [1, 1]),              // RAIN
            [0xD9] = ("CureStatus", [1, 1]),              // CLEARSTATUS
            [0xDA] = ("RunSPSCodeSimple", [1, 1, 2, 1, 2, 2]),// SPS2
            [0xDB] = ("EnableVictoryPose", [1, 1]),       // WINPOSE
            [0xDC] = ("Jump", []),                        // JUMP3
            [0xDE] = ("SetName", [1, 2]),                 // PLAYERNAME
            [0xDF] = ("SetObjectOvalRatio", [1]),         // OVAL
            [0xE0] = ("AddFrog", []),                     // INCFROG
            [0xE1] = ("TerminateBattle", []),             // BEND
            [0xE2] = ("SetupJump", [2, 2, 2, 1]),         // SETVY3
            [0xE3] = ("SetDialogProgression", [1]),       // SETSIGNAL
            [0xE4] = ("MoveTileLoopWithOffset", [1, 1, 2, 2, 1]),// BGLSCROLLOFFSET
            [0xE5] = ("AttackSpecial", [1]),              // BTLSEQ
            [0xE6] = ("SetTileLoopType", [1, 1]),         // BGLLOOPTYPE
            [0xE7] = ("SetTileAnimationFrame", [1, 1]),   // BGAFRAME
            [0xE8] = ("SideWalkXZY", [2, 2, 2]),          // MOVE3H
            [0xE9] = ("UpdatePartyUID", []),              // SYNCPARTY
            [0xEA] = ("CalculateScreenOrigin", []),       // VRP
            [0xEB] = ("CloseAllWindows", []),             // CLOSEALL
            [0xEC] = ("FadeFilter", [1, 1, 1, 1, 1, 1]),  // WIPERGB
            [0xED] = ("SetTileLoopAlpha", [1, 2, 2]),     // BGVALPHA
            [0xEE] = ("EnableInactiveAnimation", []),     // SLEEPON
            [0xEF] = ("ShowHereIcon", [1]),               // HEREON
            [0xF0] = ("EnableRun", []),                   // DASHON
            [0xF3] = ("UnlearnAbility", [1, 1]),          // CLEARAP
            [0xF4] = ("LearnAbility", [1, 1]),            // MAXAP
            [0xF5] = ("GameOver", []),                    // GAMEOVER
            [0xF6] = ("VibrateController", [1]),          // VIBSTART
            [0xF7] = ("ActivateVibration", [1]),          // VIBACTIVE
            [0xF8] = ("RunVibrationTrack", [1, 1, 1]),    // VIBTRACK1
            [0xF9] = ("ActivateVibrationTrack", [1, 1, 1]),// VIBTRACK
            [0xFA] = ("SetVibrationSpeed", [2]),          // VIBRATE
            [0xFB] = ("SetVibrationFlags", [1]),          // VIBFLAG
            [0xFC] = ("SetVibrationRange", [1, 1]),       // VIBRANGE
            [0xFD] = ("PreloadField", [1, 2]),            // HINT
            [0x102] = ("0x102", [1, 1]),                  // BSACTIVE
            [0x103] = ("0x103", [1, 1]),                  // BSFLAG
            [0x104] = ("0x104", [1, 1]),                  // BSFLOOR
            [0x105] = ("0x105", [1, 2]),                  // BSRATE
            [0x106] = ("0x106", [1, 1]),                  // BSALGO
            [0x107] = ("0x107", [1, 2, 2]),               // BSDELTA
            [0x108] = ("0x108", [1, 1]),                  // BSAXIS
            [0x10A] = ("0x10A", [1, 1]),                  // BAFRAME
            [0x10D] = ("BARATE", [1, 2]),                 // BARATE
            [0x110] = ("BARANGE", [1, 1, 1]),             // BARANGE
            [0x111] = ("BAVISIBLE", [1, 1]),              // BAVISIBLE
            [0x112] = ("SetCharacterEquipment", [3, 3, 3]),// PLAYER_EQUIP
            [0x113] = ("SetCharacterLevel", [3, 3]),      // PLAYER_LEVEL
            [0x115] = ("AddShopItem", [3, 3, 3]),         // SHOP_ITEM
            [0x116] = ("AddShopSynthesis", [3, 3, 3]),    // SHOP_SYNTH
            [0x117] = ("WalkEx", [3, 3, 3, 3, 3, 3]),     // MOVE_EX
            [0x118] = ("TurnTowardObjectEx", [3, 3, 3]),  // TURN_OBJ_EX
            [0x119] = ("SetLogicalAnimationEx", [3, 3, 3]),// AANIM_EX
            [0x11A] = ("ClearMemoriaVector", [3]),        // VECTOR_CLEAR
            [0x11B] = ("ClearMemoriaDictionary", [3]),    // DICTIONARY_CLEAR
            [0x11C] = ("SetTilePositionTimed", [3, 3, 3, 3, 3]),// BGLMOVE_TIMED
            [0x11D] = ("ADD_STATUS", [3, 3, 3, 3, 3, 3]), // ADD_STATUS
            [0x11E] = ("REMOVE_STATUS", [3, 3, 3]),       // REMOVE_STATUS
        };

    /// <summary>Opcodes whose case in the reference touches NO state a script can observe.
    /// A headless VM may step over these, evaluating their expression arguments (which assign)
    /// and discarding the values.
    /// 
    /// "Observable" is measured against two things, not guessed. First the save and the field
    /// engine's own EventContext - UCOFF writes _context.usercontrol, which scripts read back
    /// through Sysvar 2. Second the object properties EBin.getvobj exposes, which are exactly
    /// position, angle, flags, level and animation frame: DIRE and TURN write an angle a script
    /// can read, so they are NOT presentation however much they look it.
    /// 
    /// 185 opcodes. Anything absent still throws, which is the safe direction.</summary>
    public static readonly IReadOnlySet<Int32> Presentation = new HashSet<Int32>
    {
        0x00, 0x07, 0x08, 0x10, 0x12, 0x16, 0x18, 0x1A, 0x1D, 0x1E, 0x21, 0x25, 0x26, 0x27,
        0x28, 0x2B, 0x33, 0x34, 0x35, 0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F, 0x41,
        0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x4A, 0x4E, 0x4F, 0x50, 0x52, 0x53, 0x55, 0x56,
        0x58, 0x59, 0x5A, 0x5C, 0x5D, 0x5E, 0x5F, 0x60, 0x63, 0x64, 0x65, 0x66, 0x69, 0x6B,
        0x6F, 0x70, 0x71, 0x72, 0x73, 0x74, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B, 0x7C, 0x7F,
        0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x88, 0x89, 0x8B, 0x8E, 0x8F, 0x91, 0x92,
        0x94, 0x97, 0x98, 0x99, 0x9A, 0x9B, 0x9C, 0x9D, 0x9F, 0xA0, 0xA1, 0xA2, 0xA3, 0xA5,
        0xA6, 0xA7, 0xA8, 0xA9, 0xAD, 0xAE, 0xAF, 0xB3, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA,
        0xBB, 0xBC, 0xBE, 0xBF, 0xC0, 0xC1, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9,
        0xCA, 0xCB, 0xCC, 0xCD, 0xD0, 0xD1, 0xD2, 0xD3, 0xD7, 0xDA, 0xDF, 0xE1, 0xE2, 0xE3,
        0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xEB, 0xEC, 0xED, 0xEF, 0xF5, 0xF6, 0xF7,
        0xF8, 0xF9, 0xFA, 0xFB, 0xFC, 0xFD, 0x100, 0x101, 0x102, 0x103, 0x104, 0x105, 0x106,
        0x107, 0x108, 0x109, 0x10A, 0x10B, 0x10C, 0x10D, 0x10E, 0x10F, 0x110, 0x111, 0x115,
        0x116, 0x117, 0x119, 0x11C, 0x11D, 0x11E
    };
}
