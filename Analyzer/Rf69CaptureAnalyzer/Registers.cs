﻿// ReSharper disable UnusedMember.Global
namespace Rf69CaptureAnalyzer;

internal enum Registers : byte
{
    Fifo = 0x00,
    OpMode,
    DataModul,
    BitrateMsb,
    BitrateLsb,
    FdevMsb,
    FdevLsb,
    FrfMsb,
    FrfMid,
    FrfLsb,
    Osc1,
    AfcCtrl,
    Reserved0C,
    Listen1,
    Listen2,
    Listen3,
    Version,
    PaLevel,
    PaRamp,
    Ocp,
    Reserved14,
    Reserved15,
    Reserved16,
    Reserved17,
    Lna,
    RxBw,
    AfcBw,
    OokPeak,
    OokAvg,
    OokFix,
    AfcFei,
    AfcMsb,
    AfcLsb,
    FeiMsb,
    FeiLsb,
    RssiConfig,
    RssiValue,
    DioMapping1,
    DioMapping2,
    IrqFlags1,
    IrqFlags2,
    RssiThresh,
    RxTimeout1,
    RxTimeout2,
    PreambleMsb,
    PreambleLsb,
    SyncConfig,
    SyncValue1,
    SyncValue2,
    SyncValue3,
    SyncValue4,
    SyncValue5,
    SyncValue6,
    SyncValue7,
    SyncValue8,
    PacketConfig1,
    PayloadLength,
    NodeAdrs,
    BroadcastAdrs,
    AutoModes,
    FifoThresh,
    PacketConfig2,
    AesKey1,
    AesKey2,
    AesKey3,
    AesKey4,
    AesKey5,
    AesKey6,
    AesKey7,
    AesKey8,
    AesKey9,
    AesKey10,
    AesKey11,
    AesKey12,
    AesKey13,
    AesKey14,
    AesKey15,
    AesKey16,
    Temp1,
    Temp2,
    Unused50,
    Unused51,
    Unused52,
    Unused53,
    Unused54,
    Unused55,
    Unused56,
    Unused57,
    TestLna,
    Unused59,
    TestPa1,
    Unused5B,
    TestPa2,
    Unused5D,
    Unused5E,
    Unused5F,
    Unused60,
    Unused61,
    Unused62,
    Unused63,
    Unused64,
    Unused65,
    Unused66,
    Unused67,
    Unused68,
    Unused69,
    Unused6A,
    Unused6B,
    Unused6C,
    Unused6D,
    Unused6E,
    TestDagc,
    Unused70,
    TestAfc
};
