﻿Imports System.Runtime.InteropServices
Imports System.Threading.Thread
Imports System.Net.IPAddress
Imports MvCamCtrl.NET

Module ConvertPixelType

    Sub Main()
        Dim dev As MyCamera = New MyCamera
        Dim Info As String
        Dim nRet As Int32 = MyCamera.MV_OK
        Do While (True)
            Dim stDeviceInfoList As MyCamera.MV_CC_DEVICE_INFO_LIST = New MyCamera.MV_CC_DEVICE_INFO_LIST

            ' ch:枚举设备 | en:Enum device
            nRet = dev.MV_CC_EnumDevices_NET((MyCamera.MV_GIGE_DEVICE Or MyCamera.MV_USB_DEVICE), stDeviceInfoList)
            If MyCamera.MV_OK <> nRet Then
                Console.WriteLine("Enum Device failed:{0:x8}", nRet)
                Exit Do
            End If

            If (0 = stDeviceInfoList.nDeviceNum) Then
                Console.WriteLine("No Find Gige | Usb Device !")
                Exit Do
            End If

            ' ch:打印设备信息 | en:Print device info
            Dim i As Int32
            For i = 0 To stDeviceInfoList.nDeviceNum - 1
                Dim stDeviceInfo As MyCamera.MV_CC_DEVICE_INFO = New MyCamera.MV_CC_DEVICE_INFO
                stDeviceInfo = CType(Marshal.PtrToStructure(stDeviceInfoList.pDeviceInfo(i), GetType(MyCamera.MV_CC_DEVICE_INFO)), MyCamera.MV_CC_DEVICE_INFO)
                If (MyCamera.MV_GIGE_DEVICE = stDeviceInfo.nTLayerType) Then
                    Dim stGigeInfoPtr As IntPtr = Marshal.AllocHGlobal(216)
                    Marshal.Copy(stDeviceInfo.SpecialInfo.stGigEInfo, 0, stGigeInfoPtr, 216)
                    Dim stGigeInfo As MyCamera.MV_GIGE_DEVICE_INFO
                    stGigeInfo = CType(Marshal.PtrToStructure(stGigeInfoPtr, GetType(MyCamera.MV_GIGE_DEVICE_INFO)), MyCamera.MV_GIGE_DEVICE_INFO)
                    Dim nIpByte1 As UInt32 = (stGigeInfo.nCurrentIp And &HFF000000) >> 24
                    Dim nIpByte2 As UInt32 = (stGigeInfo.nCurrentIp And &HFF0000) >> 16
                    Dim nIpByte3 As UInt32 = (stGigeInfo.nCurrentIp And &HFF00) >> 8
                    Dim nIpByte4 As UInt32 = (stGigeInfo.nCurrentIp And &HFF)

                    Info = "DEV[" + Convert.ToString(i) + "] NAME[" + stGigeInfo.chUserDefinedName + "]IP[" + nIpByte1.ToString() + "." + nIpByte2.ToString() + "." + nIpByte3.ToString() + "." + nIpByte4.ToString() + "]"
                    Console.WriteLine(Info)
                Else
                    Dim stUsbInfoPtr As IntPtr = Marshal.AllocHGlobal(540)
                    Marshal.Copy(stDeviceInfo.SpecialInfo.stUsb3VInfo, 0, stUsbInfoPtr, 540)
                    Dim stUsbInfo As MyCamera.MV_USB3_DEVICE_INFO
                    stUsbInfo = CType(Marshal.PtrToStructure(stUsbInfoPtr, GetType(MyCamera.MV_USB3_DEVICE_INFO)), MyCamera.MV_USB3_DEVICE_INFO)
                    Info = "DEV[" + Convert.ToString(i) + "] NAME[" + stUsbInfo.chUserDefinedName + "]Model[" + stUsbInfo.chSerialNumber + "]"
                    Console.WriteLine(Info)
                End If
            Next

            Console.WriteLine("please select a device")
            Dim nIndex As Int32
            Try
                nIndex = Console.ReadLine()
            Catch ex As Exception
                Console.WriteLine("Invalid input!")
                Console.WriteLine("push enter to exit")
                System.Console.ReadLine()
                End
            End Try

            If nIndex > stDeviceInfoList.nDeviceNum - 1 Then
                Console.WriteLine("Invalid input!")
                Console.WriteLine("push enter to exit")
                System.Console.ReadLine()
                End
            End If

            If nIndex < 0 Then
                Console.WriteLine("Invalid input!")
                Console.WriteLine("push enter to exit")
                System.Console.ReadLine()
                End
            End If


            Dim stdevInfo As MyCamera.MV_CC_DEVICE_INFO
            stdevInfo = CType(Marshal.PtrToStructure(stDeviceInfoList.pDeviceInfo(nIndex), GetType(MyCamera.MV_CC_DEVICE_INFO)), MyCamera.MV_CC_DEVICE_INFO)

            ' ch:创建句柄 | en:Create handle
            nRet = dev.MV_CC_CreateDevice_NET(stdevInfo)
            If 0 <> nRet Then
                Console.WriteLine("Create device failed:{0:x8}", nRet)
                Exit Do
            End If

            ' ch:打开相机 | en:Open device
            nRet = dev.MV_CC_OpenDevice_NET()
            If 0 <> nRet Then
                Console.WriteLine("Open device failed:{0:x8}", nRet)
                Exit Do
            End If

            ' ch:探测网络最佳包大小(只对GigE相机有效) | en:Detection network optimal package size(It only works for the GigE camera)
            If stdevInfo.nTLayerType = MyCamera.MV_GIGE_DEVICE Then
                Dim nPacketSize As Int32
                nPacketSize = dev.MV_CC_GetOptimalPacketSize_NET()
                If nPacketSize > 0 Then
                    nRet = dev.MV_CC_SetIntValue_NET("GevSCPSPacketSize", nPacketSize)
                    If 0 <> nRet Then
                        Console.WriteLine("Warning: Set Packet Size failed:{0:x8}", nRet)
                    End If
                Else
                    Console.WriteLine("Warning: Get Packet Size failed:{0:x8}", nPacketSize)
                End If
            End If

            ' ch:开启取流 | en:Start grabbing
            nRet = dev.MV_CC_StartGrabbing_NET()
            If 0 <> nRet Then
                Console.WriteLine("Start grabbing fauled:{0:x8}", nRet)
                Exit Do
            End If


            ' ch:获取包大小 || en: Get Payload Size
            Dim stParam As MyCamera.MVCC_INTVALUE = New MyCamera.MVCC_INTVALUE()
            nRet = dev.MV_CC_GetIntValue_NET("PayloadSize", stParam)
            If (MyCamera.MV_OK <> nRet) Then
                Console.WriteLine("Get PayloadSize failed:{0:x8}", nRet)
                Exit Do
            End If
            Dim nPayloadSize As Int32 = stParam.nCurValue
            Dim pBufForDriver As IntPtr = Marshal.AllocHGlobal(nPayloadSize)
            Dim pDataForRGB As IntPtr = Marshal.AllocHGlobal(nPayloadSize * 3 + 2048)
            Dim frameInfo As MyCamera.MV_FRAME_OUT_INFO_EX = New MyCamera.MV_FRAME_OUT_INFO_EX
            Dim m_byteImageBuffer(nPayloadSize * 3 + 2048) As Byte
            Dim m_byteImageBufferLen As Int32 = nPayloadSize * 3 + 2048

            ' ch:抓取图像 | en:Get image
            nRet = dev.MV_CC_GetOneFrameTimeout_NET(pBufForDriver, nPayloadSize, frameInfo, 1000)
            If MyCamera.MV_OK = nRet Then
                Console.WriteLine("Width:" + Convert.ToString(frameInfo.nWidth) + " Height:" + Convert.ToString(frameInfo.nHeight) + " FrameNum:" + Convert.ToString(frameInfo.nFrameNum))

                Dim stConvertParam As MyCamera.MV_PIXEL_CONVERT_PARAM = New MyCamera.MV_PIXEL_CONVERT_PARAM()
                stConvertParam.nHeight = frameInfo.nHeight
                stConvertParam.nWidth = frameInfo.nWidth
                stConvertParam.pSrcData = pBufForDriver
                stConvertParam.nSrcDataLen = frameInfo.nFrameLen
                stConvertParam.enSrcPixelType = frameInfo.enPixelType
                stConvertParam.enDstPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Packed
                stConvertParam.pDstBuffer = pDataForRGB
                stConvertParam.nDstBufferSize = nPayloadSize * 3 + 2048

                nRet = dev.MV_CC_ConvertPixelType_NET(stConvertParam)
                If MyCamera.MV_OK <> nRet Then
                    Console.WriteLine("Convert to RGB failed:{0:x8}", nRet)
                    Exit Do
                End If

                ' ch:将图像数据保存到本地文件 | en:Save image data to local file
                If stConvertParam.nDstLen <> m_byteImageBufferLen Then
                    m_byteImageBufferLen = stConvertParam.nDstLen
                    ReDim m_byteImageBuffer(m_byteImageBufferLen)
                End If
                Marshal.Copy(pDataForRGB, m_byteImageBuffer, 0, m_byteImageBufferLen)
                Try
                    My.Computer.FileSystem.WriteAllBytes("Image.raw", m_byteImageBuffer, False)
                Catch ex As Exception
                End Try
            Else
                Console.WriteLine("Get one frame failed:{0:x8}", nRet)
                Exit Do
            End If

            Marshal.FreeHGlobal(pBufForDriver)
            Marshal.FreeHGlobal(pDataForRGB)

            ' ch:停止取流 | en:Stop grabbing
            nRet = dev.MV_CC_StopGrabbing_NET()
            If 0 <> nRet Then
                Console.WriteLine("Stop Grabbing failed:{0:x8}", nRet)
                Exit Do
            End If

            ' ch:关闭相机 | en:Close device
            nRet = dev.MV_CC_CloseDevice_NET()
            If 0 <> nRet Then
                Console.WriteLine("Open device failed:{0:x8}", nRet)
                Exit Do
            End If

            ' ch:销毁句柄 | en:Destroy handle
            nRet = dev.MV_CC_DestroyDevice_NET()
            If 0 <> nRet Then
                Console.WriteLine("Destroy device failed:{0:x8}", nRet)
                Exit Do
            End If

            Exit Do
        Loop

        If 0 <> nRet Then
            ' ch:销毁句柄 | en:Destroy handle
            nRet = dev.MV_CC_DestroyDevice_NET()
            If 0 <> nRet Then
                Console.WriteLine("Destroy device failed:{0:x8}", nRet)
            End If
        End If


        Console.WriteLine("Press enter to exit")
        System.Console.ReadLine()
    End Sub

End Module
