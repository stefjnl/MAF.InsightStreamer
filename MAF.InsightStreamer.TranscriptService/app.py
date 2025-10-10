from flask import Flask, jsonify, request
from youtube_transcript_api import YouTubeTranscriptApi
from youtube_transcript_api._errors import (
    TranscriptsDisabled,
    NoTranscriptFound,
    VideoUnavailable,
    NoTranscriptAvailable,
    NotTranslatable,
    TranslationLanguageNotAvailable,
    CookiePathInvalid,
    CookiesInvalid,
    YouTubeRequestFailed,
    TooManyRequests
)
import logging
import traceback
import time
import random
import requests
import os
from urllib3.util.retry import Retry
from requests.adapters import HTTPAdapter
import xml.etree.ElementTree as ET
import re
from requests.exceptions import Timeout, ConnectionError

app = Flask(__name__)
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Set custom headers for YouTube API requests
import youtube_transcript_api._api as api
api._HEADERS = {
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
}

# Realistic browser User-Agent headers to bypass YouTube's anti-bot measures
USER_AGENTS = [
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
    'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36',
    'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15',
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/120.0'
]

def create_session_with_headers():
    """Create a requests session with proper headers and retry logic"""
    user_agent = random.choice(USER_AGENTS)
    headers = {
        'User-Agent': user_agent,
        'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8',
        'Accept-Language': 'en-US,en;q=0.5',
        'Accept-Encoding': 'gzip, deflate, br',
        'DNT': '1',
        'Connection': 'keep-alive',
        'Upgrade-Insecure-Requests': '1',
        'Sec-Fetch-Dest': 'document',
        'Sec-Fetch-Mode': 'navigate',
        'Sec-Fetch-Site': 'none',
        'Cache-Control': 'max-age=0',
        'Sec-CH-UA': '"Not_A Brand";v="8", "Chromium";v="120", "Google Chrome";v="120"',
        'Sec-CH-UA-Mobile': '?0',
        'Sec-CH-UA-Platform': '"Windows"'
    }
    
    session = requests.Session()
    session.headers.update(headers)
    
    # Configure retry strategy
    retry_strategy = Retry(
        total=3,
        backoff_factor=1,
        status_forcelist=[429, 500, 502, 503, 504],
        allowed_methods=["HEAD", "GET", "OPTIONS"]
    )
    
    adapter = HTTPAdapter(max_retries=retry_strategy)
    session.mount("http://", adapter)
    session.mount("https://", adapter)
    
    logger.info(f"Created session with User-Agent: {user_agent}")
    return session

def create_error_response(error_message, error_code, status_code, video_id=None, is_retryable=None, retry_after=None):
    """
    Create a structured error response with appropriate metadata
    
    Args:
        error_message: Human-readable error message
        error_code: Machine-readable error code
        status_code: HTTP status code
        video_id: YouTube video ID (optional)
        is_retryable: Boolean indicating if error is retryable (optional)
        retry_after: Seconds to wait before retrying (optional)
    
    Returns:
        Tuple of (response_dict, status_code)
    """
    response = {
        'error': error_message,
        'error_code': error_code,
        'video_id': video_id
    }
    
    if is_retryable is not None:
        response['is_retryable'] = is_retryable
    
    if retry_after is not None:
        response['retry_after'] = retry_after
    
    return response, status_code

def is_rate_limit_error(exception):
    """
    Check if an exception is related to YouTube rate limiting
    
    Args:
        exception: The exception to check
    
    Returns:
        Boolean indicating if this is a rate limit error
    """
    error_message = str(exception).lower()
    rate_limit_indicators = [
        'too many requests',
        'rate limit',
        'quota exceeded',
        '429',
        'try again later',
        'temporarily unavailable'
    ]
    return any(indicator in error_message for indicator in rate_limit_indicators)

def is_transient_error(exception):
    """
    Check if an exception is related to transient network issues
    
    Args:
        exception: The exception to check
    
    Returns:
        Boolean indicating if this is a transient error
    """
    if isinstance(exception, (Timeout, ConnectionError)):
        return True
    
    error_message = str(exception).lower()
    transient_indicators = [
        'timeout',
        'connection',
        'network',
        'temporary',
        'service unavailable',
        '503',
        '502',
        '504'
    ]
    return any(indicator in error_message for indicator in transient_indicators)

def extract_video_info(video_id):
    """Extract video information to verify video exists and get transcript data"""
    try:
        session = create_session_with_headers()
        
        # First, try to get the video page to verify it exists
        video_url = f"https://www.youtube.com/watch?v={video_id}"
        response = session.get(video_url)
        
        if response.status_code != 200:
            logger.error(f"Video page returned status {response.status_code}")
            return None
            
        # Look for caption data in the page
        caption_match = re.search(r'"captionTracks":\[(.*?)\]', response.text)
        if not caption_match:
            logger.warning(f"No caption tracks found for video {video_id}")
            return None
            
        return True
        
    except Exception as e:
        logger.error(f"Error extracting video info: {str(e)}")
        return None

def get_transcript_direct(video_id, languages):
    """Try to get transcript using direct API approach"""
    try:
        session = create_session_with_headers()
        
        # Try different transcript API endpoints
        base_urls = [
            f"https://video.google.com/timedtext?v={video_id}",
            f"https://www.youtube.com/api/timedtext?v={video_id}"
        ]
        
        for lang in languages:
            for base_url in base_urls:
                try:
                    url = f"{base_url}&lang={lang}&fmt=json3"
                    logger.info(f"Trying direct transcript URL: {url}")
                    
                    response = session.get(url)
                    if response.status_code == 200 and response.text.strip():
                        # Parse JSON response
                        try:
                            import json
                            data = json.loads(response.text)
                            if 'events' in data:
                                transcript = []
                                for event in data['events']:
                                    if 'segs' in event:
                                        for seg in event['segs']:
                                            if 'utf8' in seg:
                                                transcript.append({
                                                    'text': seg['utf8'],
                                                    'start': event.get('tStartMs', 0) / 1000,
                                                    'duration': event.get('dDurationMs', 0) / 1000
                                                })
                                return transcript
                        except json.JSONDecodeError:
                            # Try XML format
                            url = f"{base_url}&lang={lang}&fmt=srv1"
                            response = session.get(url)
                            if response.status_code == 200 and response.text.strip():
                                try:
                                    root = ET.fromstring(response.text)
                                    transcript = []
                                    for text in root.findall('.//text'):
                                        transcript.append({
                                            'text': text.text or '',
                                            'start': float(text.get('start', 0)),
                                            'duration': float(text.get('dur', 0))
                                        })
                                    return transcript
                                except ET.ParseError:
                                    continue
                                    
                except Exception as e:
                    logger.warning(f"Direct transcript attempt failed: {str(e)}")
                    continue
                    
        return None
        
    except Exception as e:
        logger.error(f"Error in direct transcript fetch: {str(e)}")
        return None

def get_transcript_with_retry(video_id, languages, max_retries=3):
    """
    Fetch transcript with retry logic and exponential backoff
    
    Args:
        video_id: YouTube video ID
        languages: List of language codes
        max_retries: Maximum number of retry attempts
        
    Returns:
        Transcript data or raises exception
    """
    for attempt in range(max_retries):
        try:
            # Add small random delay to avoid rate limiting
            if attempt > 0:
                delay = (2 ** attempt) + random.uniform(0, 1)
                logger.info(f"Retry attempt {attempt + 1} for video {video_id}, waiting {delay:.2f}s")
                time.sleep(delay)
            
            logger.info(f"Fetching transcript for video: {video_id}, languages: {languages}, attempt: {attempt + 1}")
            
            # Use the standard YouTubeTranscriptApi with proper headers
            transcript = YouTubeTranscriptApi.get_transcript(
                video_id,
                languages=languages,
                proxies=None,
                cookies=None
            )
            logger.info(f"Successfully retrieved {len(transcript)} transcript segments for video: {video_id}")
            return transcript
            
        except Exception as e:
            logger.warning(f"Attempt {attempt + 1} failed for video {video_id}: {str(e)}")
            
            # If this is the last attempt, re-raise the exception
            if attempt == max_retries - 1:
                logger.error(f"All {max_retries} attempts failed for video {video_id}")
                raise
            
            # For specific errors, don't retry
            if isinstance(e, (TranscriptsDisabled, NoTranscriptFound, VideoUnavailable)):
                logger.error(f"Non-retryable error for video {video_id}: {str(e)}")
                raise

@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    return jsonify({'status': 'healthy', 'service': 'transcript-service'}), 200

@app.route('/transcript/<video_id>', methods=['GET'])
def get_transcript(video_id):
    """
    Extract transcript for a YouTube video.
    
    Query parameters:
    - languages: Comma-separated list of language codes (default: 'en')
    
    Returns:
    - JSON array of transcript segments with 'text', 'start', 'duration'
    """
    try:
        # Get language preference from query params
        languages = request.args.get('languages', 'en').split(',')
        
        logger.info(f"Processing transcript request for video: {video_id}, languages: {languages}")
        
        # Use list_transcripts -> find_transcript -> fetch pattern (more reliable)
        try:
            transcript_list = YouTubeTranscriptApi.list_transcripts(video_id)
            transcript_obj = transcript_list.find_transcript(languages)
            transcript = transcript_obj.fetch()
            logger.info(f"Successfully retrieved {len(transcript)} transcript segments for video: {video_id}")
        except Exception as e:
            logger.error(f"Failed to fetch transcript for video {video_id}: {str(e)}")
            raise
        
        return jsonify({
            'video_id': video_id,
            'transcript': transcript,
            'segment_count': len(transcript)
        }), 200
        
    # Business logic errors - non-retryable (404)
    except TranscriptsDisabled:
        logger.warning(f"Transcripts disabled for video: {video_id}")
        response, status = create_error_response(
            'Transcripts are disabled for this video',
            'TRANSCRIPTS_DISABLED',
            404,
            video_id,
            is_retryable=False
        )
        return jsonify(response), status
        
    except NoTranscriptFound:
        logger.warning(f"No transcript found for video: {video_id}")
        response, status = create_error_response(
            'No transcript available in requested languages',
            'NO_TRANSCRIPT_FOUND',
            404,
            video_id,
            is_retryable=False
        )
        return jsonify(response), status
        
    except NoTranscriptAvailable:
        logger.warning(f"No transcript available for video: {video_id}")
        response, status = create_error_response(
            'No transcript available for this video',
            'NO_TRANSCRIPT_AVAILABLE',
            404,
            video_id,
            is_retryable=False
        )
        return jsonify(response), status
        
    except VideoUnavailable:
        logger.warning(f"Video unavailable: {video_id}")
        response, status = create_error_response(
            'Video is unavailable or does not exist',
            'VIDEO_UNAVAILABLE',
            404,
            video_id,
            is_retryable=False
        )
        return jsonify(response), status
        
        
    # Rate limiting errors - non-retryable by Polly (429)
    except YouTubeRequestFailed as e:
        if is_rate_limit_error(e):
            logger.error(f"Rate limit exceeded for video {video_id}: {str(e)}")
            response, status = create_error_response(
                'YouTube rate limit exceeded. Please try again later.',
                'RATE_LIMIT_EXCEEDED',
                429,
                video_id,
                is_retryable=False,
                retry_after=60  # Suggest retry after 60 seconds
            )
            return jsonify(response), status
        else:
            # Fall through to transient error handling
            logger.error(f"YouTube API request failed for video {video_id}: {str(e)}")
            response, status = create_error_response(
                'YouTube API temporarily unavailable',
                'YOUTUBE_API_ERROR',
                503,
                video_id,
                is_retryable=True,
                retry_after=30
            )
            return jsonify(response), status
            
    # Transient network errors - retryable (503)
    except (Timeout, ConnectionError) as e:
        logger.error(f"Network error fetching transcript for {video_id}: {str(e)}")
        response, status = create_error_response(
            'Network timeout or connection error',
            'NETWORK_ERROR',
            503,
            video_id,
            is_retryable=True,
            retry_after=15
        )
        return jsonify(response), status
        
    # Other transient errors - retryable (503)
    except Exception as e:
        # Check if this is a transient error
        if is_transient_error(e):
            logger.error(f"Transient error fetching transcript for {video_id}: {str(e)}")
            response, status = create_error_response(
                'Service temporarily unavailable',
                'TRANSIENT_ERROR',
                503,
                video_id,
                is_retryable=True,
                retry_after=30
            )
            return jsonify(response), status
        else:
            # Unexpected server error - retryable but limited (500)
            logger.error(f"Unexpected error fetching transcript for {video_id}: {str(e)}")
            logger.error(f"Full traceback: {traceback.format_exc()}")
            response, status = create_error_response(
                'Internal server error',
                'INTERNAL_ERROR',
                500,
                video_id,
                is_retryable=True
            )
            return jsonify(response), status

@app.route('/transcript/list/<video_id>', methods=['GET'])
def list_transcripts(video_id):
    """
    List all available transcript languages for a video.
    
    Returns:
    - JSON array of available language codes
    """
    try:
        # Skip video verification and try to list transcripts directly
        transcript_list = YouTubeTranscriptApi.list_transcripts(video_id)
        
        available = []
        for transcript in transcript_list:
            available.append({
                'language': transcript.language,
                'language_code': transcript.language_code,
                'is_generated': transcript.is_generated,
                'is_translatable': transcript.is_translatable
            })
        
        return jsonify({
            'video_id': video_id,
            'available_transcripts': available
        }), 200
        
    # Business logic errors - non-retryable (404)
    except TranscriptsDisabled:
        logger.warning(f"Transcripts disabled for video: {video_id}")
        response, status = create_error_response(
            'Transcripts are disabled for this video',
            'TRANSCRIPTS_DISABLED',
            404,
            video_id,
            is_retryable=False
        )
        return jsonify(response), status
        
    except NoTranscriptFound:
        logger.warning(f"No transcript found for video: {video_id}")
        response, status = create_error_response(
            'No transcript available for this video',
            'NO_TRANSCRIPT_FOUND',
            404,
            video_id,
            is_retryable=False
        )
        return jsonify(response), status
        
    except NoTranscriptAvailable:
        logger.warning(f"No transcript available for video: {video_id}")
        response, status = create_error_response(
            'No transcript available for this video',
            'NO_TRANSCRIPT_AVAILABLE',
            404,
            video_id,
            is_retryable=False
        )
        return jsonify(response), status
        
    except VideoUnavailable:
        logger.warning(f"Video unavailable: {video_id}")
        response, status = create_error_response(
            'Video is unavailable or does not exist',
            'VIDEO_UNAVAILABLE',
            404,
            video_id,
            is_retryable=False
        )
        return jsonify(response), status
        
    except VideoRegionBlocked:
        logger.warning(f"Video region blocked for: {video_id}")
        response, status = create_error_response(
            'Video is blocked in your region',
            'VIDEO_REGION_BLOCKED',
            404,
            video_id,
            is_retryable=False
        )
        return jsonify(response), status
        
    # Rate limiting errors - non-retryable by Polly (429)
    except YouTubeRequestFailed as e:
        if is_rate_limit_error(e):
            logger.error(f"Rate limit exceeded for video {video_id}: {str(e)}")
            response, status = create_error_response(
                'YouTube rate limit exceeded. Please try again later.',
                'RATE_LIMIT_EXCEEDED',
                429,
                video_id,
                is_retryable=False,
                retry_after=60
            )
            return jsonify(response), status
        else:
            # Fall through to transient error handling
            logger.error(f"YouTube API request failed for video {video_id}: {str(e)}")
            response, status = create_error_response(
                'YouTube API temporarily unavailable',
                'YOUTUBE_API_ERROR',
                503,
                video_id,
                is_retryable=True,
                retry_after=30
            )
            return jsonify(response), status
            
    # Transient network errors - retryable (503)
    except (Timeout, ConnectionError) as e:
        logger.error(f"Network error listing transcripts for {video_id}: {str(e)}")
        response, status = create_error_response(
            'Network timeout or connection error',
            'NETWORK_ERROR',
            503,
            video_id,
            is_retryable=True,
            retry_after=15
        )
        return jsonify(response), status
        
    # Other transient errors - retryable (503)
    except Exception as e:
        # Check if this is a transient error
        if is_transient_error(e):
            logger.error(f"Transient error listing transcripts for {video_id}: {str(e)}")
            response, status = create_error_response(
                'Service temporarily unavailable',
                'TRANSIENT_ERROR',
                503,
                video_id,
                is_retryable=True,
                retry_after=30
            )
            return jsonify(response), status
        else:
            # Unexpected server error - retryable but limited (500)
            logger.error(f"Unexpected error listing transcripts for {video_id}: {str(e)}")
            logger.error(f"Full traceback: {traceback.format_exc()}")
            response, status = create_error_response(
                'Internal server error',
                'INTERNAL_ERROR',
                500,
                video_id,
                is_retryable=True
            )
            return jsonify(response), status

if __name__ == '__main__':
    # Get port from environment variable (set by Aspire) or use default
    port = int(os.environ.get('PORT', 7279))
    app.run(host='0.0.0.0', port=port, debug=False)