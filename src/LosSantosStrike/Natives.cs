using GTA.Native;

namespace LosSantosStrike
{
    /// <summary>Native function hashes this script calls (names from the GTA V native reference).</summary>
    internal static class N
    {
        public const Hash GET_FOLLOW_PED_CAM_VIEW_MODE = (Hash)0x8D4D46230B2C353A;
        public const Hash SET_FOLLOW_PED_CAM_VIEW_MODE = (Hash)0x5A4F9EDF1673F704;
        public const Hash GET_CAM_ACTIVE_VIEW_MODE_CONTEXT = (Hash)0x19CAFA3C87F7C2FF;
        public const Hash GET_CAM_VIEW_MODE_FOR_CONTEXT = (Hash)0xEE778F8C7E1142E2;
        public const Hash SET_CAM_VIEW_MODE_FOR_CONTEXT = (Hash)0x2A2173E46DAECD12;
        public const Hash SET_CINEMATIC_BUTTON_ACTIVE = (Hash)0x51669F7D1FB53D9F;

        public const Hash SET_WEAPON_DAMAGE_MODIFIER = (Hash)0x4757F00BC6323CFE;
        public const Hash GET_WEAPON_DAMAGE = (Hash)0x3133B907D8B32053;
        public const Hash MAKE_PED_RELOAD = (Hash)0x20AE33F3AC9C0033;
        public const Hash IS_PED_RELOADING = (Hash)0x24B100C68C645951;
        public const Hash IS_PED_SHOOTING = (Hash)0x34616828CD07F1A1;
        public const Hash IS_PED_ARMED = (Hash)0x475768A975D5AD17;
        public const Hash DOES_WEAPON_TAKE_WEAPON_COMPONENT = (Hash)0x5CEE3DF569CECAB0;
        public const Hash HAS_PED_GOT_WEAPON_COMPONENT = (Hash)0xC593212475FAE340;
        public const Hash GIVE_WEAPON_COMPONENT_TO_PED = (Hash)0xD966D51AA5B28BB9;
        public const Hash REMOVE_WEAPON_COMPONENT_FROM_PED = (Hash)0x1E8BE90C74FB4C09;

        public const Hash IS_PED_ON_FOOT = (Hash)0x01FEE67DB37F59B2;
        public const Hash IS_PED_FALLING = (Hash)0xFB92A102F1C4DFA3;
        public const Hash IS_PED_JUMPING = (Hash)0xCEDABC5900A0BF97;
        public const Hash IS_ENTITY_IN_AIR = (Hash)0x886E37EC497200B6;
        public const Hash IS_PED_CLIMBING = (Hash)0x53E8CB4F48BFE623;
        public const Hash IS_PED_VAULTING = (Hash)0x117C70D1F5730B5E;
        public const Hash IS_PED_IN_COVER = (Hash)0x60DFD0691A170B88;
        public const Hash IS_PED_GOING_INTO_COVER = (Hash)0x9F65DBC537E59AD5;
        public const Hash IS_PED_SWIMMING = (Hash)0x9DE327631295B4C2;
        public const Hash IS_PED_RAGDOLL = (Hash)0x47E4E977581C5B55;
        public const Hash IS_PED_GETTING_INTO_A_VEHICLE = (Hash)0xBB062B2B5722478E;
        public const Hash IS_PED_IN_MELEE_COMBAT = (Hash)0x4E209B2C1EAD5159;
        public const Hash GET_PED_PARACHUTE_STATE = (Hash)0x79CFD9827CC979B6;
        public const Hash IS_PED_USING_ANY_SCENARIO = (Hash)0x57AB4A3080F85143;
        public const Hash IS_PED_DEAD_OR_DYING = (Hash)0x3317DEDB88C95038;
        public const Hash GET_PED_STEALTH_MOVEMENT = (Hash)0x7C2AC9CA66575FBF;
        public const Hash GET_IS_TASK_ACTIVE = (Hash)0xB0760331C7AA4155;
        public const Hash IS_PED_IN_ANY_VEHICLE = (Hash)0x997ABD671D25CA0B;
        public const Hash SET_PED_MOVE_RATE_OVERRIDE = (Hash)0x085BF80FA50A39D1;

        public const Hash IS_PLAYER_CONTROL_ON = (Hash)0x49C32D60007AFA47;
        public const Hash IS_PLAYER_FREE_AIMING = (Hash)0x2E397FD2ECD37C87;
        public const Hash IS_PLAYER_BEING_ARRESTED = (Hash)0x388A47C51ABDAC8E;
        public const Hash IS_PLAYER_SWITCH_IN_PROGRESS = (Hash)0xD9D2CFFF49FAB35F;
        public const Hash IS_CUTSCENE_PLAYING = (Hash)0xD3C2E180A40F031E;
        public const Hash IS_PAUSE_MENU_ACTIVE = (Hash)0xB0034A223497FFCB;
        public const Hash IS_DISABLED_CONTROL_PRESSED = (Hash)0xE2587F8CBBD87B1D;

        public const int TaskClimbLadder = 1;
        public const int CamViewFirstPerson = 4;
    }
}
