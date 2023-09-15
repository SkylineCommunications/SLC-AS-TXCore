# TECHX-AS-TXCore
This repository contains the necessary automation scripts for the deployment of the TXCore DataMiner Low-Code Application.

The solution contains the following automation scripts.

## MWCore_StatisticsEnable
Allows the user to enable the statistics interface for a specific TXCore server.

## MWCore_ProcessStreamAlarms
Used to process stream alarms for providing a summary, TS, IP and historical alarm. In the overall low-code app solution, this is integrated with a DataMiner correlation rule.

## MWCore_PauseResume
Provides an easy way to pause or resume a specific source or output.

## MWCore_DownloadThumbnails
Download the thumbnails for the available stream. In the overall low-code app solution, and to keep the thumbnails up to date, this automation script is integrated with a DataMiner scheduler task.

## MWCore_AdHoc-E2E-StreamsMWCore_AdHoc-E2E-Streams
This script implements a DataMiner Generic Query Interface (GQI), enabling the end-to-end overview in a DataMiner Dashboard or Low-code app.