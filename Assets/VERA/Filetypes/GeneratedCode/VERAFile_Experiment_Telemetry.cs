#if VERAFile_Experiment_Telemetry
using UnityEngine;
using System;

namespace VERA
{
	
	/// <summary>
	/// Static class for recording new entries to the Experiment_Telemetry CSV file.
	/// <br/><br/>This class has been generated based on the CSV file you have defined on the VERA portal.
	/// This class should be the only way you record new CSV entries to the Experiment_Telemetry file.
	/// <br/><br/>Notably, use the CreateCsvEntry() method to create new entries in the Experiment_Telemetry CSV log file.
	/// </summary>
	public static class VERAFile_Experiment_Telemetry
	{
		
		private const string fileName = "Experiment_Telemetry";
		
		/// <summary>
		/// Creates a new row entry in the Experiment_Telemetry CSV log file.
		/// This file is automatically populated and handled by VERA; researchers should NOT need to call this function directly.
		public static void CreateCsvEntry(bool headsetDetected, float headsetVirtualPosX, float headsetVirtualPosY, float headsetVirtualPosZ, float headsetVirtualRotEulerX, float headsetVirtualRotEulerY, float headsetVirtualRotEulerZ, float headsetVirtualRotQuatX, float headsetVirtualRotQuatY, float headsetVirtualRotQuatZ, float headsetVirtualRotQuatW, float headsetTrackingPosX, float headsetTrackingPosY, float headsetTrackingPosZ, float headsetTrackingRotEulerX, float headsetTrackingRotEulerY, float headsetTrackingRotEulerZ, float headsetTrackingRotQuatX, float headsetTrackingRotQuatY, float headsetTrackingRotQuatZ, float headsetTrackingRotQuatW, bool leftDetected, float leftControllerVirtualPosX, float leftControllerVirtualPosY, float leftControllerVirtualPosZ, float leftControllerVirtualRotEulerX, float leftControllerVirtualRotEulerY, float leftControllerVirtualRotEulerZ, float leftControllerVirtualRotQuatX, float leftControllerVirtualRotQuatY, float leftControllerVirtualRotQuatZ, float leftControllerVirtualRotQuatW, float leftControllerTrackingPosX, float leftControllerTrackingPosY, float leftControllerTrackingPosZ, float leftControllerTrackingRotEulerX, float leftControllerTrackingRotEulerY, float leftControllerTrackingRotEulerZ, float leftControllerTrackingRotQuatX, float leftControllerTrackingRotQuatY, float leftControllerTrackingRotQuatZ, float leftControllerTrackingRotQuatW, float leftTrigger, float leftGrip, int leftPrimaryButton, int leftSecondaryButton, int leftPrimary2DAxisClick, float leftThumbstickX, float leftThumbstickY, bool rightDetected, float rightControllerVirtualPosX, float rightControllerVirtualPosY, float rightControllerVirtualPosZ, float rightControllerVirtualRotEulerX, float rightControllerVirtualRotEulerY, float rightControllerVirtualRotEulerZ, float rightControllerVirtualRotQuatX, float rightControllerVirtualRotQuatY, float rightControllerVirtualRotQuatZ, float rightControllerVirtualRotQuatW, float rightControllerTrackingPosX, float rightControllerTrackingPosY, float rightControllerTrackingPosZ, float rightControllerTrackingRotEulerX, float rightControllerTrackingRotEulerY, float rightControllerTrackingRotEulerZ, float rightControllerTrackingRotQuatX, float rightControllerTrackingRotQuatY, float rightControllerTrackingRotQuatZ, float rightControllerTrackingRotQuatW, float rightTrigger, float rightGrip, int rightPrimaryButton, int rightSecondaryButton, int rightPrimary2DAxisClick, float rightThumbstickX, float rightThumbstickY)
		{
			VERASessionManager.CreateArbitraryCsvEntry(fileName, headsetDetected, headsetVirtualPosX, headsetVirtualPosY, headsetVirtualPosZ, headsetVirtualRotEulerX, headsetVirtualRotEulerY, headsetVirtualRotEulerZ, headsetVirtualRotQuatX, headsetVirtualRotQuatY, headsetVirtualRotQuatZ, headsetVirtualRotQuatW, headsetTrackingPosX, headsetTrackingPosY, headsetTrackingPosZ, headsetTrackingRotEulerX, headsetTrackingRotEulerY, headsetTrackingRotEulerZ, headsetTrackingRotQuatX, headsetTrackingRotQuatY, headsetTrackingRotQuatZ, headsetTrackingRotQuatW, leftDetected, leftControllerVirtualPosX, leftControllerVirtualPosY, leftControllerVirtualPosZ, leftControllerVirtualRotEulerX, leftControllerVirtualRotEulerY, leftControllerVirtualRotEulerZ, leftControllerVirtualRotQuatX, leftControllerVirtualRotQuatY, leftControllerVirtualRotQuatZ, leftControllerVirtualRotQuatW, leftControllerTrackingPosX, leftControllerTrackingPosY, leftControllerTrackingPosZ, leftControllerTrackingRotEulerX, leftControllerTrackingRotEulerY, leftControllerTrackingRotEulerZ, leftControllerTrackingRotQuatX, leftControllerTrackingRotQuatY, leftControllerTrackingRotQuatZ, leftControllerTrackingRotQuatW, leftTrigger, leftGrip, leftPrimaryButton, leftSecondaryButton, leftPrimary2DAxisClick, leftThumbstickX, leftThumbstickY, rightDetected, rightControllerVirtualPosX, rightControllerVirtualPosY, rightControllerVirtualPosZ, rightControllerVirtualRotEulerX, rightControllerVirtualRotEulerY, rightControllerVirtualRotEulerZ, rightControllerVirtualRotQuatX, rightControllerVirtualRotQuatY, rightControllerVirtualRotQuatZ, rightControllerVirtualRotQuatW, rightControllerTrackingPosX, rightControllerTrackingPosY, rightControllerTrackingPosZ, rightControllerTrackingRotEulerX, rightControllerTrackingRotEulerY, rightControllerTrackingRotEulerZ, rightControllerTrackingRotQuatX, rightControllerTrackingRotQuatY, rightControllerTrackingRotQuatZ, rightControllerTrackingRotQuatW, rightTrigger, rightGrip, rightPrimaryButton, rightSecondaryButton, rightPrimary2DAxisClick, rightThumbstickX, rightThumbstickY);
		}
		
	}
}
#endif
